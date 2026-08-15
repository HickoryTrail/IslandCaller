using System.IO.Compression;
using System.Text.Json;
using ClassIsland.Shared;
using IslandCaller.Models;

namespace IslandCaller.Services;

/// <summary>
/// 导入和导出 IslandCaller 设置及 AppData 数据。
/// 此服务无状态，不依赖 DI 容器，调用方可直接实例化。
/// </summary>
public sealed class SettingsTransferService
{
    private const string SettingsEntryName = "settings.json";
    private const string AppDataEntryPrefix = "AppData/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string AppDataRootPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IslandCaller");

    /// <summary>
    /// 将当前设置和完整 AppData 打包到目标流。
    /// </summary>
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var settingsEntry = archive.CreateEntry(SettingsEntryName, CompressionLevel.Optimal);
        await using (var settingsStream = settingsEntry.Open())
        {
            await JsonSerializer.SerializeAsync(settingsStream, Settings.Instance, JsonOptions, cancellationToken);
        }

        if (!Directory.Exists(AppDataRootPath))
        {
            archive.CreateEntry(AppDataEntryPrefix);
            return;
        }

        AddDirectoryEntries(archive, AppDataRootPath, AppDataEntryPrefix);
    }

    /// <summary>
    /// 从数据包导入设置和完整 AppData。任何失败都会恢复导入前状态。
    /// </summary>
    public async Task ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var appDataParent = Path.GetDirectoryName(AppDataRootPath)
                            ?? throw new InvalidOperationException("无法确定 AppData 目录。");
        Directory.CreateDirectory(appDataParent);

        var operationId = Guid.NewGuid().ToString("N");
        var stagingRoot = Path.Combine(appDataParent, $".IslandCaller-import-{operationId}");
        var stagingAppData = Path.Combine(stagingRoot, "AppData");
        var backupPath = Path.Combine(appDataParent, $".IslandCaller-backup-{operationId}");

        SettingsModel importedSettings;
        try
        {
            Directory.CreateDirectory(stagingAppData);
            importedSettings = await ReadAndExtractAsync(source, stagingAppData, cancellationToken);
            ValidateRequiredData(importedSettings, stagingAppData);
        }
        catch
        {
            DeleteDirectoryIfExists(stagingRoot);
            throw;
        }

        var oldSettings = Settings.Instance;
        var settingsStore = new Settings(IAppHost.GetService<ProfileService>());
        var oldAppDataMoved = false;
        var newAppDataMoved = false;

        try
        {
            if (Directory.Exists(AppDataRootPath))
            {
                Directory.Move(AppDataRootPath, backupPath);
                oldAppDataMoved = true;
            }

            Directory.Move(stagingAppData, AppDataRootPath);
            newAppDataMoved = true;

            settingsStore.ReplaceModel(importedSettings);

            DeleteDirectoryIfExists(backupPath);
            DeleteDirectoryIfExists(stagingRoot);
        }
        catch (Exception importException)
        {
            Exception? rollbackException = null;

            try
            {
                if (newAppDataMoved)
                {
                    DeleteDirectoryIfExists(AppDataRootPath);
                }

                if (oldAppDataMoved && Directory.Exists(backupPath))
                {
                    Directory.Move(backupPath, AppDataRootPath);
                }
            }
            catch (Exception exception)
            {
                rollbackException = exception;
            }

            try
            {
                settingsStore.ReplaceModel(oldSettings);
            }
            catch (Exception exception)
            {
                rollbackException = rollbackException == null
                    ? exception
                    : new AggregateException(rollbackException, exception);
            }

            DeleteDirectoryIfExists(stagingRoot);

            if (rollbackException != null)
            {
                throw new AggregateException("导入失败且回滚过程中发生错误。", importException, rollbackException);
            }

            throw new InvalidOperationException("导入失败，原有数据已恢复。", importException);
        }
    }

    private static void AddDirectoryEntries(ZipArchive archive, string rootPath, string archivePrefix)
    {
        archive.CreateEntry(archivePrefix);

        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootPath, directory).Replace('\\', '/');
            archive.CreateEntry($"{archivePrefix}{relativePath}/");
        }

        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{archivePrefix}{relativePath}", CompressionLevel.Optimal);
        }
    }

    private static async Task<SettingsModel> ReadAndExtractAsync(
        Stream source,
        string stagingAppData,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry? settingsEntry = null;
        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            var name = NormalizeEntryName(entry.FullName);
            if (!normalizedNames.Add(name))
            {
                throw new InvalidDataException($"数据包包含重复路径：{name}");
            }

            if (name.Equals(SettingsEntryName, StringComparison.OrdinalIgnoreCase))
            {
                if (settingsEntry != null)
                {
                    throw new InvalidDataException("数据包包含多个 settings.json。");
                }

                settingsEntry = entry;
                continue;
            }

            if (!name.Equals("AppData", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith(AppDataEntryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"数据包包含不支持的路径：{name}");
            }

            var relativePath = name.Equals("AppData", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : name[AppDataEntryPrefix.Length..];
            var targetPath = GetSafePath(stagingAppData, relativePath);

            if (IsDirectoryEntry(entry))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var targetDirectory = Path.GetDirectoryName(targetPath)
                                  ?? throw new InvalidDataException($"无效的文件路径：{name}");
            Directory.CreateDirectory(targetDirectory);
            await using var input = entry.Open();
            await using var output = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
        }

        if (settingsEntry == null)
        {
            throw new InvalidDataException("数据包缺少 settings.json。");
        }

        await using var settingsStream = settingsEntry.Open();
        var settings = await JsonSerializer.DeserializeAsync<SettingsModel>(
            settingsStream,
            JsonOptions,
            cancellationToken);

        if (settings == null)
        {
            throw new InvalidDataException("settings.json 为空或不是有效的设置对象。");
        }

        NormalizeSettings(settings);
        return settings;
    }

    private static void NormalizeSettings(SettingsModel settings)
    {
        settings.General ??= new GeneralSetting();
        settings.Profile ??= new ProfileSetting();
        settings.Hover ??= new HoverSetting();
        settings.TTS ??= new TTSSetting();
        settings.Call ??= new CallSettings();

        settings.Profile.ProfileList ??= new Dictionary<Guid, string>();
        settings.Profile.ProfilePrefer ??= new Dictionary<Guid, Guid>();
        settings.Hover.Position ??= new PositionSetting();
    }

    private static void ValidateRequiredData(SettingsModel settings, string stagedAppData)
    {
        if (settings.Profile.DefaultProfile == Guid.Empty)
        {
            throw new InvalidDataException("设置中缺少有效的默认档案。");
        }

        var defaultProfilePath = Path.Combine(
            stagedAppData,
            "Profile",
            $"{settings.Profile.DefaultProfile}.csv");
        if (!File.Exists(defaultProfilePath))
        {
            throw new InvalidDataException("数据包缺少默认档案文件。");
        }
    }

    private static string NormalizeEntryName(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"数据包包含不安全路径：{entryName}");
        }

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains('\0'))
        {
            throw new InvalidDataException("数据包包含无效路径。");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or "..") ||
            (segments.Length > 0 && segments[0].Contains(':')))
        {
            throw new InvalidDataException($"数据包包含不安全路径：{entryName}");
        }

        return string.Join('/', segments);
    }

    private static string GetSafePath(string rootPath, string relativePath)
    {
        var fullRoot = Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, fullRoot[..^1], StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"数据包包含越界路径：{relativePath}");
        }

        return fullPath;
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
