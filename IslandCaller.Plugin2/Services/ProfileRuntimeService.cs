using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

/// <summary>
/// 统一协调依赖当前档案的运行时状态。
/// </summary>
public sealed class ProfileRuntimeService
{
    private readonly object reloadLock = new();
    private readonly ProfileService profileService;
    private readonly HistoryService historyService;
    private readonly CoreService coreService;
    private readonly Status status;
    private readonly ILogger<ProfileRuntimeService> logger;

    public ProfileRuntimeService(
        ProfileService profileService,
        HistoryService historyService,
        CoreService coreService,
        Status status,
        ILogger<ProfileRuntimeService> logger)
    {
        this.profileService = profileService;
        this.historyService = historyService;
        this.coreService = coreService;
        this.status = status;
        this.logger = logger;
    }

    /// <summary>
    /// 按要求的顺序初始化依赖档案的服务。
    /// </summary>
    internal void Initialize()
    {
        lock (reloadLock)
        {
            profileService.Initialize();
            historyService.Initialize();
            coreService.Initialize();
        }
    }

    /// <summary>
    /// 将档案、历史和计算权重作为一个整体进行重载。
    /// </summary>
    internal void Reload(Guid profileId)
    {
        lock (reloadLock)
        {
            ReloadCore(profileId);
        }
    }

    /// <summary>
    /// 确保指定档案处于活动状态。当前状态检查也在锁内，避免并发入口
    /// 同时通过检查并重复开始重载。
    /// </summary>
    internal bool EnsureLoaded(Guid profileId)
    {
        lock (reloadLock)
        {
            if (IsLoaded(profileId))
            {
                return true;
            }

            try
            {
                ReloadCore(profileId);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "加载名单 {ProfileGuid} 失败。", profileId);
                return false;
            }
        }
    }

    private bool IsLoaded(Guid profileId)
    {
        return profileService.ActiveProfileId == profileId &&
               historyService.ActiveProfileId == profileId &&
               status.ProfileServiceInitialized &&
               status.HistoryServiceInitialized &&
               status.CoreServiceInitialized;
    }

    private void ReloadCore(Guid profileId)
    {
        profileService.LoadSelectedProfile(profileId);
        historyService.Load(profileId);
        coreService.Initialize();
        logger.LogInformation("已切换至名单 {ProfileGuid}。", profileId);
    }
}
