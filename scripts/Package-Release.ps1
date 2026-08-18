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
    dotnet restore 'IslandCaller.Plugin2\IslandCaller.Plugin2.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Failed to restore IslandCaller.Plugin2.' }

    dotnet build 'IslandCaller.Plugin2\IslandCaller.Plugin2.csproj' --configuration Release --no-restore --nologo `
        '-p:EnableWindowsTargeting=true' "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw 'Failed to build IslandCaller.Plugin2.' }

    $pluginBuildDirectory = Join-Path $repositoryRoot "IslandCaller.Plugin2\bin\Release\$targetFramework"
    if (-not (Test-Path -LiteralPath $pluginBuildDirectory -PathType Container)) {
        throw "Plugin build output not found: $pluginBuildDirectory"
    }

    $pluginBuildFiles = @(Get-ChildItem -LiteralPath $pluginBuildDirectory -File | Where-Object { $_.Extension -ne '.pdb' })
    if ($pluginBuildFiles.Count -eq 0) {
        throw "Plugin build output is empty: $pluginBuildDirectory"
    }
    Copy-Item -LiteralPath $pluginBuildFiles.FullName -Destination $pluginDirectory
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
