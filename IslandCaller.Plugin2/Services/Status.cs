using ReactiveUI;

namespace IslandCaller.Services
{
    public class Status : ReactiveObject
    {
        private bool _profileServiceInitialized;
        public bool ProfileServiceInitialized
        {
            get => _profileServiceInitialized;
            set => this.RaiseAndSetIfChanged(ref _profileServiceInitialized, value);
        }

        private bool _historyServiceInitialized;
        public bool HistoryServiceInitialized
        {
            get => _historyServiceInitialized;
            set => this.RaiseAndSetIfChanged(ref _historyServiceInitialized, value);
        }

        private bool _coreServiceInitialized;
        public bool CoreServiceInitialized
        {
            get => _coreServiceInitialized;
            set => this.RaiseAndSetIfChanged(ref _coreServiceInitialized, value);
        }

        private bool _islandCallerServiceInitialized;
        public bool IslandCallerServiceInitialized
        {
            get => _islandCallerServiceInitialized;
            set => this.RaiseAndSetIfChanged(ref _islandCallerServiceInitialized, value);
        }

        private bool _isTimeStatusAvailable;
        public bool IsTimeStatusAvailable
        {
            get => _isTimeStatusAvailable;
            set => this.RaiseAndSetIfChanged(ref _isTimeStatusAvailable, value);
        }

        private bool _occupationDisable;
        public bool OccupationDisable
        {
            get => _occupationDisable;
            set => this.RaiseAndSetIfChanged(ref _occupationDisable, value);
        }

        private bool _interruptionEnable;
        public bool InterruptionEnable
        {
            get => _interruptionEnable;
            set => this.RaiseAndSetIfChanged(ref _interruptionEnable, value);
        }

        private bool _isPluginReady;
        public bool IsPluginReady
        {
            get => _isPluginReady;
            private set => this.RaiseAndSetIfChanged(ref _isPluginReady, value);
        }

        public Status()
        {
            ProfileServiceInitialized = false;
            HistoryServiceInitialized = false;
            CoreServiceInitialized = false;
            IslandCallerServiceInitialized = false;
            InterruptionEnable = false;
            IsTimeStatusAvailable = false;
            OccupationDisable = true;

            this.WhenAnyValue(
                x => x.ProfileServiceInitialized,
                x => x.HistoryServiceInitialized,
                x => x.CoreServiceInitialized,
                x => x.IslandCallerServiceInitialized,
                x => x.InterruptionEnable,
                x => x.IsTimeStatusAvailable,
                x => x.OccupationDisable,
                (a, b, c, d, e, f, g) => a && b && c && d && f && (g || e)
            )
            .BindTo(this, x => x.IsPluginReady);
        }

    }
}
