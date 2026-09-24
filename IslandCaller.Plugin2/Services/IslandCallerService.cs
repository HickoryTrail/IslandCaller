using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Core.Controls;
using ClassIsland.Shared;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;
using IslandCaller.Helpers;
using IslandCaller.Services.NotificationProvidersNew;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services.IslandCallerService
{
    public class IslandCallerService
    {
        private ILessonsService? LessonsService { get; set; }
        private IProfileService? ClassIslandProfileService { get; set; }
        private IUriNavigationService? UriNavigationService { get; set; }
        private ILogger<IslandCallerService>? Logger { get; }
        private CancellationTokenSource Cts {  get; set; }

        private CoreService CoreService { get; set; }
        private HistoryService HistoryService { get; set; }
        private ProfileService ProfileService { get; set; }
        private ProfileRuntimeService ProfileRuntimeService { get; set; }
        // 故意用 object 而非 IOmniTTS：避免 IslandCallerService 的其它方法在 JIT 时
        // 被迫加载可选的 OmniTTS.Shared 程序集（该程序集可能被系统策略拦截）。
        private object? OmniTTS { get; set; }
        private ISpeechService? ClassIslandTTS { get; set; }
        private WindowsManager WindowsManager { get; set; }
        private IslandCallerNotificationProviderNew? NotificationProvider { get; set; }
        public Status Status { get; set; }
        public IslandCallerService(ILogger<IslandCallerService> logger)
        {
            Logger = logger;
            Logger?.LogTrace("IslandCallerService created.");
        }

        internal void Initialize()
        {
            HistoryService = IAppHost.GetService<HistoryService>();
            CoreService = IAppHost.GetService<CoreService>();
            ProfileService = IAppHost.GetService<ProfileService>();
            ProfileRuntimeService = IAppHost.GetService<ProfileRuntimeService>();
            Status = IAppHost.GetService<Status>();
            WindowsManager = IAppHost.GetService<WindowsManager>();
            NotificationProvider = IAppHost.TryGetService<IslandCallerNotificationProviderNew>()
                ?? new IslandCallerNotificationProviderNew();
            // 获取服务
            LessonsService = IAppHost.TryGetService<ILessonsService>();
            ClassIslandProfileService = IAppHost.TryGetService<IProfileService>();
            UriNavigationService = IAppHost.TryGetService<IUriNavigationService>();
            ClassIslandTTS = IAppHost.TryGetService<ISpeechService>();

            Status.IslandCallerServiceInitialized = false;
            Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & (LessonsService?.CurrentState ?? TimeState.OnClass) == TimeState.Breaking);
            Status.InterruptionEnable = Settings.Instance.General.Interruptable;

            // 检查设置项是否有效
            // OmniTTS 是可选依赖：用安全桥接获取，未安装 / 文件缺失 / 被系统安全策略
            // （如 Windows 11「智能应用控制」）拦截时降级处理，而不是让插件初始化直接崩溃。
            OmniTTS = OmniTtsBridge.TryResolve();
            if (Settings.Instance.TTS.Provider == IslandCaller.TtsProvider.OmniTTS && OmniTTS is null)
            {
                Settings.Instance.TTS.Provider = IslandCaller.TtsProvider.None;
                Logger?.LogWarning("OmniTTS 不可用（未安装或依赖被系统安全策略拦截），TTS 提供方已自动回退为「无」。");
            }

            if (Settings.Instance.Profile.IsPreferProfile)
            {
                ApplyProfileForCurrentLesson(clearThisLessonHistory: false);
            }

            // 订阅设置变更
            LessonsService?.CurrentTimeStateChanged += (s, e) =>
            {
                Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & (LessonsService?.CurrentState ?? TimeState.OnClass) == TimeState.Breaking);
                if (Settings.Instance.Profile.IsPreferProfile)
                {
                    ApplyProfileForCurrentLesson(clearThisLessonHistory: true);
                }
                else
                {
                    HistoryService.ClearThisLessonHistory();
                }
            };
            Settings.Instance.General.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.Instance.General.BreakDisable))
                {
                    Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & (LessonsService?.CurrentState ?? TimeState.OnClass) == TimeState.Breaking);
                }
                if (e.PropertyName == nameof(Settings.Instance.General.Interruptable))
                {
                    Status.InterruptionEnable = Settings.Instance.General.Interruptable;
                }
            };
            Settings.Instance.Hover.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.Instance.Hover.IsEnable))
                {
                    if (Settings.Instance.Hover.IsEnable) WindowsManager.ShowHoverWindow();
                    else WindowsManager.CloseHoverWindow();
                }
            };
            Settings.Instance.Profile.PropertyChanged += ProfileSettingsOnPropertyChanged;
            UriNavigationService?.HandlePluginsNavigation(
                "IslandCaller/Simple",
                args => ShowRandomStudent(1)
            );
            UriNavigationService?.HandlePluginsNavigation(
                "IslandCaller/Advanced/GUI",
                args =>
                {
                    new PersonalCall().Show();
                }
            );
            Status.IslandCallerServiceInitialized = true;
            Logger?.LogInformation("IslandCallerService initialized.");
        }

        private void ProfileSettingsOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (ProfileService.ActiveProfileId != Guid.Empty &&
                !Settings.Instance.Profile.ProfileList.ContainsKey(ProfileService.ActiveProfileId))
            {
                SwitchToDefaultProfile(clearThisLessonHistory: true);
                return;
            }

            if (Settings.Instance.Profile.IsPreferProfile &&
                e.PropertyName is nameof(ProfileSetting.IsPreferProfile) or nameof(ProfileSetting.ProfilePrefer) or
                nameof(ProfileSetting.DefaultProfile) or nameof(ProfileSetting.ProfileList))
            {
                ApplyProfileForCurrentLesson(clearThisLessonHistory: true);
            }
        }

        private void ApplyProfileForCurrentLesson(bool clearThisLessonHistory)
        {
            Guid defaultProfileId = Settings.Instance.Profile.DefaultProfile;
            Guid targetProfileId = ResolvePreferredProfileId();

            if (!TryLoadProfile(targetProfileId) && targetProfileId != defaultProfileId)
            {
                TryLoadProfile(defaultProfileId);
            }

            if (clearThisLessonHistory)
            {
                HistoryService.ClearThisLessonHistory();
            }
        }

        private void SwitchToDefaultProfile(bool clearThisLessonHistory)
        {
            TryLoadProfile(Settings.Instance.Profile.DefaultProfile);
            if (clearThisLessonHistory)
            {
                HistoryService.ClearThisLessonHistory();
            }
        }

        private Guid ResolvePreferredProfileId()
        {
            Guid defaultProfileId = Settings.Instance.Profile.DefaultProfile;
            object? classIslandProfile = ClassIslandProfileService?.Profile;
            if (!Settings.Instance.Profile.IsPreferProfile ||
                LessonsService?.CurrentState != TimeState.OnClass ||
                LessonsService.CurrentSubject is not { } currentSubject ||
                classIslandProfile is null)
            {
                return defaultProfileId;
            }

            Guid subjectId = ClassIslandSubjectHelper.FindSubjectId(classIslandProfile, currentSubject);
            if (subjectId == Guid.Empty ||
                !Settings.Instance.Profile.ProfilePrefer.TryGetValue(subjectId, out Guid preferredProfileId) ||
                !Settings.Instance.Profile.ProfileList.ContainsKey(preferredProfileId))
            {
                return defaultProfileId;
            }

            return preferredProfileId;
        }

        private bool TryLoadProfile(Guid profileId)
        {
            if (!Settings.Instance.Profile.ProfileList.ContainsKey(profileId))
            {
                Logger?.LogWarning("名单 {ProfileGuid} 不在当前设置中，已跳过加载。", profileId);
                return false;
            }

            return ProfileRuntimeService.EnsureLoaded(profileId);
        }

        /// <summary>
        /// 由全局快捷键触发一次随机点名。
        /// 与悬浮窗「Call」按钮共用 <see cref="ShowRandomStudent"/>：同一名单档案、同一防重复均衡权重算法、
        /// 同一展示渠道（通知 / 展示窗口）与 TTS 播报设置。
        /// </summary>
        public void TriggerRandomCallFromHotkey()
        {
            if (Status.IsPluginReady == false)
            {
                Logger?.LogWarning("快捷键触发忽略：插件尚未就绪。");
                return;
            }

            // 重复触发保护：上一次展示仍在进行且用户未开启「允许打断」时，忽略本次触发，避免结果互相覆盖。
            if (!Status.OccupationDisable && !Status.InterruptionEnable)
            {
                Logger?.LogInformation("快捷键触发忽略：上一次点名仍在展示且未开启打断。");
                return;
            }

            Logger?.LogInformation("全局快捷键触发点名。");
            ShowRandomStudent(1);
        }

        /// <summary>无可用内容（名单为空 / 无可点名成员）时的统一反馈。</summary>
        private void ShowCannotCallFeedback()
        {
            const string header = "无法点名";
            const string content = "当前名单为空或没有可点名的学生，请先在设置中导入或选择名单。";
            void Show() => _ = CommonTaskDialogs.ShowDialog(header, content);
            if (Dispatcher.UIThread.CheckAccess())
            {
                Show();
            }
            else
            {
                Dispatcher.UIThread.Post(Show);
            }
        }

        public async void ShowRandomStudent(int stunum)
        {
            // 准备点名
            if(Status.IsPluginReady == false) return;

            // 无可用内容保护：名单为空时不再产出 "Error" 文本，而是给出明确反馈。
            if (CoreService.PersonCount <= 0)
            {
                Logger?.LogWarning("点名请求被忽略：当前名单为空或没有可点名的学生。");
                ShowCannotCallFeedback();
                return;
            }

            if (Status.InterruptionEnable && (Status.OccupationDisable == false))
            {
                Cts?.Cancel();
                Cts?.Dispose();
                Logger?.LogWarning("上一个点名请求已被取消");
            }
            Status.OccupationDisable = false;
            // 获取点名数据
            List<string> students = new();
            for (int i = 0; i < stunum; i++)
            {
                students.Add(CoreService.GetRandomStudent());
            }

            string output = string.Join("  ", students);
            string speechContent = $"{Settings.Instance.TTS.BeforeText}{output}{Settings.Instance.TTS.AfterText}";
            float duration = stunum * Settings.Instance.Call.BaseTime + Settings.Instance.Call.AdditionalTime; // 计算持续时间
            if(duration <= 0)
            {
                Logger?.LogError($"点名时长小于 0: {duration}");
                speechContent = String.Empty;
                duration = 3;
                output = "Error: 点名时长小于 0";
            }

            // 发送结果
            Cts = new CancellationTokenSource();
            var thisCts = Cts;
            if (Settings.Instance.TTS.Provider == IslandCaller.TtsProvider.OmniTTS
                && !OmniTtsBridge.TryPlay(OmniTTS, speechContent, Cts.Token))
            {
                Logger?.LogWarning("OmniTTS 播报不可用，本次已跳过语音播报。");
            }
            else if (Settings.Instance.TTS.Provider == IslandCaller.TtsProvider.ClassIsland) ClassIslandTTS?.EnqueueSpeechQueue(speechContent);
            if ((Settings.Instance.Call.NotifyMethod & 0b01) != 0)
            {
                // 复用同一个已注册的提醒提供方实例发通知。不能每次 new：
                // NotificationProviderBase 构造会自动向通知中心重复注册，抽多了会越积越多导致卡死。
                if (NotificationProvider is not null)
                {
                    _ = NotificationProvider.RandomCall(output, duration, Cts.Token);
                }
            }
            if ((Settings.Instance.Call.NotifyMethod & 0b10) != 0) _ = WindowsManager.ShowCallWindowAsync(output, duration, Cts.Token);
            try
            {
                await Task.Delay((int)(duration * 1000), Cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (Cts != null && thisCts == Cts) Cts?.Dispose();
            Status.OccupationDisable = true;
        }
    }
}
