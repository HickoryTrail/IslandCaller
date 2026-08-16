using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using IslandCaller.Models;
using IslandCaller.Services;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Actions;

[ActionInfo("IslandCaller.SwitchProfile", "切换档案", "\uE9A8", false)]
public sealed class SwitchProfileAction(
    ILogger<SwitchProfileAction> logger,
    ProfileRuntimeService profileRuntimeService) : ActionBase<SwitchProfileActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings.ProfileId == Guid.Empty)
        {
            logger.LogWarning("切换档案行动未配置目标档案。");
            return;
        }

        if (!IslandCaller.Models.Settings.Instance.Profile.ProfileList.TryGetValue(
                Settings.ProfileId,
                out string? profileName))
        {
            logger.LogWarning("切换档案行动的目标档案不存在。ProfileGuid={ProfileGuid}", Settings.ProfileId);
            return;
        }

        if (!profileRuntimeService.EnsureLoaded(Settings.ProfileId))
        {
            logger.LogWarning("切换档案行动加载失败。ProfileGuid={ProfileGuid}", Settings.ProfileId);
            return;
        }

        logger.LogInformation("行动：已切换至档案 {ProfileName} ({ProfileGuid})。", profileName, Settings.ProfileId);
    }
}
