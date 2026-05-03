using IslandCaller.Services.NotificationProvidersNew;
using ClassIsland.Core.Abstractions.Services;
using IslandCaller.Views;
using ClassIsland.Shared.Enums;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services.IslandCallerService
{
    public class IslandCallerService
    {
        private ILessonsService LessonsService { get; }
        private ILogger<IslandCallerService> Logger { get; }
        private CoreService CoreService {  get; }
        private Plugin Plugin { get; set; }
        private IslandCallerNotificationProviderNew LastRequest { get; set; }
        public Status Status { get; set; }
        public IslandCallerService(Plugin plugin, 
                                    IUriNavigationService uriNavigationService, 
                                    ILessonsService lessonsService,
                                    HistoryService historyService,
                                    CoreService coreService,
                                    Status status,
                                    ILogger<IslandCallerService> logger
            )
        {
            
            LessonsService = lessonsService;
            CoreService = coreService;
            Logger = logger;
            Plugin = plugin;
            Status = status;
            status.IslandCallerServiceInitialized = false;
            Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & lessonsService.CurrentState == TimeState.Breaking);
            Status.InterruptionEnable = Settings.Instance.General.Interruptable;
            lessonsService.CurrentTimeStateChanged += (s, e) =>
            {
                historyService.ClearThisLessonHistory();
                Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & lessonsService.CurrentState == TimeState.Breaking);
            };
            Settings.Instance.General.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.Instance.General.BreakDisable))
                {
                    Status.IsTimeStatusAvailable = !(Settings.Instance.General.BreakDisable & lessonsService.CurrentState == TimeState.Breaking);
                }
                if(e.PropertyName == nameof(Settings.Instance.General.Interruptable))
                {
                    Status.InterruptionEnable = Settings.Instance.General.Interruptable;
                }
            };
            Settings.Instance.Hover.PropertyChanged += (s,e) =>
            {
                if(e.PropertyName == nameof(Settings.Instance.Hover.IsEnable))
                {
                    if (Settings.Instance.Hover.IsEnable)
                    {
                        plugin.HoverWindow = new HoverFluent();
                        plugin.HoverWindow.Show();
                    }
                    else plugin.HoverWindow.Close();
                }
            };
            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Simple",
                args => ShowRandomStudent(1)
            );
            uriNavigationService.HandlePluginsNavigation(
                "IslandCaller/Advanced/GUI",
                args =>
                {
                    new PersonalCall().Show();
                }
            );
            status.IslandCallerServiceInitialized = true;
        }

        public async void ShowRandomStudent(int stunum)
        {
            if(Status.IsPluginReady == false) return;
            Status.OccupationDisable = false;
            if (Status.InterruptionEnable == true && LastRequest != null)
            {
                LastRequest.Request.Cancel();
                Logger.LogWarning("上一个点名请求已被取消");
            }
            LastRequest = new IslandCallerNotificationProviderNew(LessonsService, CoreService);
            LastRequest.RandomCall(stunum);
            await Task.Delay(stunum * 2000 + 1000);
            Status.OccupationDisable = true;
            LastRequest = null;
        }
    }
}
