using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Shared;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;
using IslandCaller.Plugin2.Helpers;
using IslandCaller.Plugin2.Services;
using IslandCaller.Services.NotificationProvidersNew;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;
using OmniTTS.Shared;

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
        private IOmniTTS? OmniTTS { get; set; }
        private ISpeechService? ClassIslandTTS { get; set; }
        private WindowsManager WindowsManager { get; set; }
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
            // 获取服务
            LessonsService = IAppHost.TryGetService<ILessonsService>();
            ClassIslandProfileService = IAppHost.TryGetService<IProfileService>();
            UriNavigationService = IAppHost.TryGetService<IUriNavigationService>();
            ClassIslandTTS = IAppHost.TryGetService<ISpeechService>();
            OmniTTS = IAppHost.TryGetService<IOmniTTS>();

            Status.IslandCallerServiceInitialized = false;
            Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & (LessonsService?.CurrentState ?? TimeState.OnClass) == TimeState.Breaking);
            Status.InterruptionEnable = Settings.Instance.General.Interruptable;

            // 检查设置项是否有效
            if (Settings.Instance.TTS.Provider == Plugin2.TtsProvider.OmniTTS && !CheckDependences.CheckOmniTTS()) Settings.Instance.TTS.Provider = Plugin2.TtsProvider.None;
            OmniTTS = IAppHost.TryGetService<IOmniTTS>();

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

        public async void ShowRandomStudent(int stunum)
        {
            // 准备点名
            if(Status.IsPluginReady == false) return;

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

            // 发送结果
            Cts = new CancellationTokenSource(new TimeSpan(0, 0, 0, (int)duration, (int)((duration - (int)duration) * 1000)));
            var thisCts = Cts;
            if (Settings.Instance.TTS.Provider == Plugin2.TtsProvider.OmniTTS) OmniTTS?.PlayAudio(speechContent, Cts.Token);
            else if (Settings.Instance.TTS.Provider == Plugin2.TtsProvider.ClassIsland) ClassIslandTTS?.EnqueueSpeechQueue(speechContent);
            if ((Settings.Instance.Call.NotifyMethod & 0b01) != 0) new IslandCallerNotificationProviderNew().RandomCall(output, duration, Cts.Token);
            if ((Settings.Instance.Call.NotifyMethod & 0b10) != 0) WindowsManager.ShowCallWindow(output, duration, Cts.Token);
            try
            {
                await Task.Delay((int)(duration * 1000), Cts.Token);
            }
            catch { }
            if (Cts != null && thisCts == Cts) Cts?.Dispose();
            Status.OccupationDisable = true;
        }
    }
}
