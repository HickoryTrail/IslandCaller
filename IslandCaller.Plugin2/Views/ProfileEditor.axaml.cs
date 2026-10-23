using Avalonia.Controls;
using ClassIsland.Core.Controls;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Services;
using IslandCaller.ViewModels;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Plugin2;

public partial class ProfileEditor : Window
{
    private readonly ProfileEditorViewModel vm;
    private readonly ProfileService profileService;
    private readonly ProfileRuntimeService profileRuntimeService;
    private readonly ILogger<ProfileEditor> logger;
    private bool isClosingAfterSave;

    public ProfileEditor(Guid profileId)
    {
        InitializeComponent();
        vm = new ProfileEditorViewModel(profileId);
        DataContext = vm;
        profileService = IAppHost.GetService<ProfileService>();
        profileRuntimeService = IAppHost.GetService<ProfileRuntimeService>();
        logger = IAppHost.GetService<ILogger<ProfileEditor>>();
        Closed += (_, _) => vm.Detach();
    }

    private void AddButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        vm.AddStudent();
    }

    private async void ImportButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (vm.IsSettingsLocked)
        {
            return;
        }

        var members = await ProfileImportHelper.ImportAsync(this, logger);
        if (members is not null && !vm.IsSettingsLocked)
        {
            vm.ReplaceMembers(members);
        }
    }

    private async void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SaveAsync();
    }

    private async void ProfileEditor_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // 设置被 RemoteCI 锁定后直接关闭，不再自动保存。
        if (isClosingAfterSave || vm.IsSettingsLocked)
        {
            return;
        }

        e.Cancel = true;
        if (await SaveAsync())
        {
            isClosingAfterSave = true;
            Close();
        }
    }

    private async Task<bool> SaveAsync()
    {
        if (vm.IsSettingsLocked)
        {
            await CommonTaskDialogs.ShowDialog("设置已锁定", "管理员已通过 RemoteCI 锁定 IslandCaller 设置，本次修改不会被保存。", this);
            return false;
        }

        var profileName = vm.ProfileName.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            await CommonTaskDialogs.ShowDialog("名称不能为空", "请输入名单名称后再保存。", this);
            return false;
        }

        try
        {
            var members = vm.ToMembers();
            profileService.SaveProfile(vm.ProfileId, members);
            var profileList = new Dictionary<Guid, string>(Settings.Instance.Profile.ProfileList)
            {
                [vm.ProfileId] = profileName
            };
            Settings.Instance.Profile.ProfileList = profileList;
            vm.ProfileName = profileName;

            if (vm.ProfileId == profileService.ActiveProfileId)
            {
                profileRuntimeService.Reload(vm.ProfileId);
            }

            logger.LogInformation("档案保存成功，Guid: {ProfileGuid}，人数：{Count}", vm.ProfileId, members.Count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "保存档案失败，Guid: {ProfileGuid}", vm.ProfileId);
            await CommonTaskDialogs.ShowDialog("保存失败", "保存档案时发生错误，请重试。", this);
            return false;
        }
    }
}
