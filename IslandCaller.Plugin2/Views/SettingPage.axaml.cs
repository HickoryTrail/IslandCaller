using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Extensions.UI;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using IslandCaller.Models;
using IslandCaller.Plugin2;
using IslandCaller.Services;
using IslandCaller.ViewModels;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Views;

[SettingsPageInfo("plugins.IslandCaller", "IslandCaller 设置", "\uED39", "\uECF8", SettingsPageCategory.External)]
public partial class SettingPage : SettingsPageBase
{
    private readonly SettingPageViewModel vm;
    private readonly HistoryService historyService;
    private readonly ILogger<SettingPage> logger;
    private readonly SettingsTransferService transferService = new();

    public SettingPage()
    {
        InitializeComponent();
        vm = (SettingPageViewModel)DataContext!;
        historyService = IAppHost.GetService<HistoryService>();
        logger = IAppHost.GetService<ILogger<SettingPage>>();
        logger.LogInformation("SettingPage 初始化完成");
    }

    private async void CreateProfileButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (GetOwnerWindow() is not Window owner)
        {
            logger.LogError("新建名单失败：无法获取窗口上下文");
            await CommonTaskDialogs.ShowDialog("创建失败", "无法获取窗口上下文，请重试。");
            return;
        }

        var profileName = await new ProfileNameDialog().ShowDialog<string?>(owner);
        if (string.IsNullOrWhiteSpace(profileName))
        {
            logger.LogInformation("用户取消了新档案命名");
            return;
        }

        var profileId = Guid.NewGuid();
        try
        {
            var profileService = vm.ProfileService;
            profileService.CreateDemoProfile(profileId);
            var profileList = new Dictionary<Guid, string>(Settings.Instance.Profile.ProfileList)
            {
                [profileId] = profileName.Trim()
            };
            Settings.Instance.Profile.ProfileList = profileList;
            Settings.Instance.Profile.ProfileNum = Settings.Instance.Profile.ProfileList.Count;
            vm.ReloadProfiles();
            logger.LogInformation("新档案创建成功，Guid: {ProfileGuid}", profileId);
            await new ProfileEditor(profileId).ShowDialog(owner);
            vm.ReloadProfiles();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "创建新档案失败，Guid: {ProfileGuid}", profileId);
            await CommonTaskDialogs.ShowDialog("创建失败", "创建新档案时发生错误，请重试。", this);
        }
    }

    private async void EditButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid profileId } || GetOwnerWindow() is not Window owner)
        {
            return;
        }

        try
        {
            await new ProfileEditor(profileId).ShowDialog(owner);
            vm.ReloadProfiles();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "打开档案编辑器失败，Guid: {ProfileGuid}", profileId);
            await CommonTaskDialogs.ShowDialog("打开失败", "无法读取对应档案，请检查名单文件。", this);
        }
    }

    private async void DeleteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid profileId } || GetOwnerWindow() is not Window owner)
        {
            return;
        }

        var profile = Settings.Instance.Profile.ProfileList;
        if (profileId == Settings.Instance.Profile.DefaultProfile || !profile.TryGetValue(profileId, out var profileName))
        {
            return;
        }

        int ruleCount = vm.GetProfilePreferenceRuleCount(profileId);
        string rulesDescription = ruleCount == 0
            ? string.Empty
            : $"\n\n同时会移除引用此名单的 {ruleCount} 条科目规则。";

        var dialog = new FAContentDialog
        {
            Title = "移除名单",
            Content = $"确定要从档案列表中移除“{profileName}”吗？本地名单文件不会被删除。{rulesDescription}",
            PrimaryButtonText = "移除",
            SecondaryButtonText = "取消",
            DefaultButton = FAContentDialogButton.Secondary
        };
        var result = await dialog.ShowAsync(owner);
        if (result != FAContentDialogResult.Primary)
        {
            return;
        }

        vm.RemoveProfilePreferenceRulesForProfile(profileId);
        var profileList = new Dictionary<Guid, string>(profile);
        profileList.Remove(profileId);
        Settings.Instance.Profile.ProfileList = profileList;
        Settings.Instance.Profile.ProfileNum = profileList.Count;
        vm.ReloadProfiles();
        logger.LogInformation("档案已从设置列表移除，Guid: {ProfileGuid}", profileId);
    }

    private void AddProfilePreferenceRuleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        vm.AddProfilePreferenceRule();
    }

    private void DeleteProfilePreferenceRuleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: SettingPageViewModel.ProfilePreferenceItemViewModel item })
        {
            vm.RemoveProfilePreferenceRule(item);
        }
    }

    private void ClearButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        logger.LogInformation("清空点名历史记录");
        historyService.ClearThisLessonHistory();
        historyService.ClearLongTermHistory();
    }

    private async void ExportSettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this) ?? AppBase.Current.GetRootWindow();
        if (topLevel == null)
        {
            await CommonTaskDialogs.ShowDialog("导出失败", "无法获取文件选择器窗口。", this);
            return;
        }

        try
        {
            var filePath = await PlatformServices.FilePickerService.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "导出 IslandCaller 数据包",
                    SuggestedFileName = $"IslandCaller-{DateTime.Now:yyyyMMddHHmmss}.iscdoc",
                    DefaultExtension = ".iscdoc",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("IslandCaller 数据包")
                        {
                            Patterns = ["*.iscdoc"]
                        }
                    ]
                },
                topLevel);

            if (filePath == null)
            {
                return;
            }

            using var storageFile = await PlatformServices.FilePickerService.GetFileAsync(filePath, topLevel)
                                     ?? throw new FileNotFoundException("无法打开导出目标文件。", filePath);
            await using var outputStream = await storageFile.OpenWriteAsync();
            if (outputStream.CanSeek)
            {
                outputStream.SetLength(0);
                outputStream.Position = 0;
            }

            await transferService.ExportAsync(outputStream);
            await CommonTaskDialogs.ShowDialog("导出完成", "IslandCaller 设置和 AppData 已成功导出。", this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "导出 IslandCaller 数据包失败");
            await CommonTaskDialogs.ShowDialog("导出失败", ex.Message, this);
        }
    }

    private async void ImportSettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this) ?? AppBase.Current.GetRootWindow();
        if (topLevel == null)
        {
            await CommonTaskDialogs.ShowDialog("导入失败", "无法获取文件选择器窗口。", this);
            return;
        }

        try
        {
            var filePaths = await PlatformServices.FilePickerService.OpenFilesPickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "导入 IslandCaller 数据包",
                    FileTypeFilter =
                    [
                        new FilePickerFileType("IslandCaller 数据包")
                        {
                            Patterns = ["*.iscdoc"]
                        }
                    ],
                    AllowMultiple = false
                },
                topLevel);

            if (filePaths == null || filePaths.Count == 0)
            {
                return;
            }

            var confirmation = new FATaskDialog
            {
                Header = "导入 IslandCaller 数据包",
                Content = "导入将清空现有的 IslandCaller 设置、名单、历史记录以及 AppData 下的其他插件数据，并使用数据包内容完全替换。此操作不可逆，确定要继续吗？",
                XamlRoot = this,
                Buttons =
                {
                    new FATaskDialogButton("取消", false),
                    new FATaskDialogButton("继续导入", true)
                    {
                        IsDefault = true
                    }
                }
            };

            if (!Equals(await confirmation.ShowAsync(), true))
            {
                return;
            }

            var temporaryPackagePath = Path.Combine(
                Path.GetTempPath(),
                $"IslandCaller-import-{Guid.NewGuid():N}.iscdoc");
            try
            {
                using (var storageFile = await PlatformServices.FilePickerService.GetFileAsync(filePaths[0], topLevel)
                                           ?? throw new FileNotFoundException("无法打开所选数据包。", filePaths[0]))
                await using (var inputStream = await storageFile.OpenReadAsync())
                await using (var temporaryPackageStream = new FileStream(
                                 temporaryPackagePath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 bufferSize: 81920,
                                 useAsync: true))
                {
                    await inputStream.CopyToAsync(temporaryPackageStream);
                }

                await using var packageStream = new FileStream(
                    temporaryPackagePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);
                await transferService.ImportAsync(packageStream);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPackagePath))
                    {
                        File.Delete(temporaryPackagePath);
                    }
                }
                catch (Exception cleanupException)
                {
                    logger.LogWarning(cleanupException, "清理临时 IslandCaller 数据包失败：{Path}", temporaryPackagePath);
                }
            }

            await CommonTaskDialogs.ShowDialog("导入完成", "数据包已成功导入，应用将重新启动以应用设置。", this);
            RequestRestart();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "导入 IslandCaller 数据包失败");
            await CommonTaskDialogs.ShowDialog("导入失败", ex.Message, this);
        }
    }

    private Window? GetOwnerWindow()
    {
        return TopLevel.GetTopLevel(this) as Window;
    }
}
