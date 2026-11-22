[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\release'
}

$releaseDirectory = Join-Path $OutputDirectory $Version
$pluginDirectory = Join-Path $releaseDirectory 'plugin'
$cipxPath = Join-Path $releaseDirectory 'IslandCaller.Plugin2.cipx'
$releaseNoteSource = Join-Path $repositoryRoot "docs\CHANGELOG\$Version.md"
$releaseNotePath = Join-Path $releaseDirectory 'release-notes.md'
$projectPath = Join-Path $repositoryRoot 'IslandCaller.Plugin2\IslandCaller.Plugin2.csproj'
$urlHandlerProjectPath = Join-Path $repositoryRoot 'IslandCaller.UrlHandler\IslandCaller.UrlHandler.csproj'
$urlHandlerPublishDirectory = Join-Path $releaseDirectory 'urlhandler-publish'

[xml]$projectDefinition = Get-Content -LiteralPath $projectPath
$targetFramework = [string]($projectDefinition.Project.PropertyGroup |
    ForEach-Object { $_.TargetFramework } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw "No TargetFramework was found in $projectPath"
}

if (Test-Path -LiteralPath $releaseDirectory) {
    throw "Output directory already exists: $releaseDirectory. Choose a different -OutputDirectory or remove it first."
}

New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswherePath -PathType Leaf)) {
        throw 'NativeAOT requires Visual Studio 2022 or later with the Desktop development with C++ workload.'
    }
    $visualStudioInstallation = & $vswherePath -latest -products '*' `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($visualStudioInstallation)) {
        throw 'NativeAOT requires the Visual Studio C++ x64/x86 build tools. Install the Desktop development with C++ workload.'
    }

    dotnet restore 'IslandCaller.Plugin2\IslandCaller.Plugin2.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Failed to restore IslandCaller.Plugin2.' }

    dotnet build 'IslandCaller.Plugin2\IslandCaller.Plugin2.csproj' --configuration Release --no-restore --nologo `
        '-p:EnableWindowsTargeting=true' "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw 'Failed to build IslandCaller.Plugin2.' }

    dotnet restore $urlHandlerProjectPath --runtime win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Failed to restore IslandCaller.UrlHandler for win-x64.' }

    dotnet publish $urlHandlerProjectPath --configuration Release --runtime win-x64 --self-contained true `
        --no-restore --nologo "-p:PublishAot=true" "-p:Version=$Version" --output $urlHandlerPublishDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Failed to publish IslandCaller.UrlHandler for win-x64.' }

    $pluginBuildDirectory = Join-Path $repositoryRoot "IslandCaller.Plugin2\bin\Release\$targetFramework"
    if (-not (Test-Path -LiteralPath $pluginBuildDirectory -PathType Container)) {
        throw "Plugin build output not found: $pluginBuildDirectory"
    }

    $pluginBuildFiles = @(Get-ChildItem -LiteralPath $pluginBuildDirectory -File | Where-Object { $_.Extension -ne '.pdb' })
    if ($pluginBuildFiles.Count -eq 0) {
        throw "Plugin build output is empty: $pluginBuildDirectory"
    }
    Copy-Item -LiteralPath $pluginBuildFiles.FullName -Destination $pluginDirectory

    $urlHandlerExecutable = Join-Path $urlHandlerPublishDirectory 'IslandCaller.UrlHandler.exe'
    if (-not (Test-Path -LiteralPath $urlHandlerExecutable -PathType Leaf)) {
        throw "AOT-published UrlHandler executable not found: $urlHandlerExecutable"
    }
    if ((Get-Item -LiteralPath $urlHandlerExecutable).Length -eq 0) {
        throw "AOT-published UrlHandler executable is empty: $urlHandlerExecutable"
    }
    Copy-Item -LiteralPath $urlHandlerExecutable -Destination $pluginDirectory
}
finally {
    Pop-Location
}

$manifestPath = Join-Path $pluginDirectory 'manifest.yml'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Built plugin manifest not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw
if ($manifest -notmatch '(?m)^version: .*$') {
    throw "No version field was found in $manifestPath"
}
[System.IO.File]::WriteAllText(
    $manifestPath,
    [regex]::Replace($manifest, '(?m)^version: .*$', "version: $Version"),
    [System.Text.UTF8Encoding]::new($false))

$pluginContents = @(Get-ChildItem -LiteralPath $pluginDirectory -Force)
if ($pluginContents.Count -eq 0) {
    throw 'Plugin package output is empty.'
}
$zipPath = Join-Path $releaseDirectory 'IslandCaller.Plugin2.zip'
Compress-Archive -Path $pluginContents.FullName -DestinationPath $zipPath -CompressionLevel Optimal
Move-Item -LiteralPath $zipPath -Destination $cipxPath

Add-Type -AssemblyName System.IO.Compression.FileSystem
$cipxArchive = [System.IO.Compression.ZipFile]::OpenRead($cipxPath)
try {
    if (-not ($cipxArchive.Entries.FullName -contains 'IslandCaller.UrlHandler.exe')) {
        throw 'IslandCaller.UrlHandler.exe is missing from the cipx archive root.'
    }
}
finally {
    $cipxArchive.Dispose()
}

$cipxMd5 = (Get-FileHash -LiteralPath $cipxPath -Algorithm MD5).Hash.ToLowerInvariant()
if (Test-Path -LiteralPath $releaseNoteSource -PathType Leaf) {
    $releaseNote = Get-Content -LiteralPath $releaseNoteSource -Raw
} else {
    $releaseNote = "# IslandCaller $Version`r`n`r`nArtifact MD5: ``$cipxMd5```r`n"
}
$releaseNote = $releaseNote.Replace('<CIPX_MD5>', $cipxMd5)
[System.IO.File]::WriteAllText($releaseNotePath, $releaseNote, [System.Text.UTF8Encoding]::new($false))

Write-Host "Local release package created: $releaseDirectory"
Write-Host "Plugin package: $cipxPath"
Write-Host "Plugin MD5: $cipxMd5"
Write-Host "Release note: $releaseNotePath"
