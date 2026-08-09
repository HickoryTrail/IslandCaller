using Avalonia.Controls;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Extensions.UI;
using ClassIsland.Core.Helpers.UI;
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

        var dialog = new FAContentDialog
        {
            Title = "移除名单",
            Content = $"确定要从档案列表中移除“{profileName}”吗？本地名单文件不会被删除。",
            PrimaryButtonText = "移除",
            SecondaryButtonText = "取消",
            DefaultButton = FAContentDialogButton.Secondary
        };
        var result = await dialog.ShowAsync(owner);
        if (result != FAContentDialogResult.Primary)
        {
            return;
        }

        var profileList = new Dictionary<Guid, string>(profile);
        profileList.Remove(profileId);
        Settings.Instance.Profile.ProfileList = profileList;
        Settings.Instance.Profile.ProfileNum = profileList.Count;
        vm.ReloadProfiles();
        logger.LogInformation("档案已从设置列表移除，Guid: {ProfileGuid}", profileId);
    }

    private void ClearButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        logger.LogInformation("清空点名历史记录");
        historyService.ClearThisLessonHistory();
        historyService.ClearLongTermHistory();
    }

    private Window? GetOwnerWindow()
    {
        return TopLevel.GetTopLevel(this) as Window;
    }
}
