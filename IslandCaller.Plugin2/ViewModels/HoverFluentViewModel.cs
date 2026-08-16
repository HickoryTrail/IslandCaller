using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Services;
using ReactiveUI;

namespace IslandCaller.ViewModels
{
    public class HoverFluentViewModel : ReactiveObject
    {
        private double _windowScalingFactor = 1.0;
        public double WindowScalingFactor
        {
            get => _windowScalingFactor;
            set => this.RaiseAndSetIfChanged(ref _windowScalingFactor, value);
        }

        private bool _isenabled;
        public bool IsEnabled
        {
            get => _isenabled;
            set => this.RaiseAndSetIfChanged(ref _isenabled, value);
        }

        public string Glyph1 => IsEnabled ? "\uECF8" : "\uED08";
        public string Glyph2 => IsEnabled ? "\uED42" : "\uED08";

        private int _hoverLayout;
        public int HoverLayout
        {
            get => _hoverLayout;
            private set => this.RaiseAndSetIfChanged(ref _hoverLayout, value);
        }

        private double _positionX;
        public double PositionX
        {
            get => _positionX;
            set
            {
                this.RaiseAndSetIfChanged(ref _positionX, value);
                Settings.Instance.Hover.Position.X = value;
            }
        }

        private double _positionY;
        public double PositionY
        {
            get => _positionY;
            set
            {
                this.RaiseAndSetIfChanged(ref _positionY, value);
                Settings.Instance.Hover.Position.Y = value;
            }
        }

        public HoverFluentViewModel()
        {
            // 从设置加载初始值
            WindowScalingFactor = Settings.Instance.Hover.ScalingFactor;
            HoverLayout = Settings.Instance.Hover.HoverLayout;
            PositionX = Settings.Instance.Hover.Position.X;
            PositionY = Settings.Instance.Hover.Position.Y;

            // 监听设置变化
            Settings.Instance.Hover.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName is nameof(Settings.Instance.Hover.ScalingFactor)
                    or nameof(Settings.Instance.Hover.HoverLayout))
                {
                    WindowScalingFactor = Settings.Instance.Hover.ScalingFactor;
                    HoverLayout = Settings.Instance.Hover.HoverLayout;
                }
            };
            var status = IAppHost.GetService<Status>();
            IsEnabled = status.IsPluginReady;
            status.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(status.IsPluginReady))
                {
                    IsEnabled = status.IsPluginReady;
                }
            };
            this.WhenAnyValue(x => x.IsEnabled)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(Glyph1)));
            this.WhenAnyValue(x => x.IsEnabled)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(Glyph2)));
        }

    }
}
