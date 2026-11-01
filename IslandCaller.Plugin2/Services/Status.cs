using ReactiveUI;

namespace IslandCaller.Services
{
    public class Status : ReactiveObject
    {
        public bool ProfileServiceInitialized
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool HistoryServiceInitialized
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool CoreServiceInitialized
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool IslandCallerServiceInitialized
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool IsTimeStatusAvailable
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool OccupationDisable
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool InterruptionEnable
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool IsPluginReady
        {
            get;
            private set => this.RaiseAndSetIfChanged(ref field, value);
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
