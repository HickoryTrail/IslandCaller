using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Services;
using ReactiveUI;
using System.ComponentModel;

namespace IslandCaller.ViewModels
{
    public class HoverFluentViewModel : ReactiveObject, IDisposable
    {
        private readonly HoverSetting _hoverSettings;
        private readonly Status _status;
        private readonly PropertyChangedEventHandler _hoverSettingsChangedHandler;
        private readonly PropertyChangedEventHandler _statusChangedHandler;
        private readonly IDisposable _glyph1Subscription;
        private readonly IDisposable _glyph2Subscription;
        private bool _disposed;

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
            _hoverSettings = Settings.Instance.Hover;
            WindowScalingFactor = _hoverSettings.ScalingFactor;
            HoverLayout = _hoverSettings.HoverLayout;
            PositionX = _hoverSettings.Position.X;
            PositionY = _hoverSettings.Position.Y;

            // 监听设置变化
            _hoverSettingsChangedHandler = OnHoverSettingsChanged;
            _hoverSettings.PropertyChanged += _hoverSettingsChangedHandler;
            _status = IAppHost.GetService<Status>();
            IsEnabled = _status.IsPluginReady;
            _statusChangedHandler = OnStatusChanged;
            _status.PropertyChanged += _statusChangedHandler;
            _glyph1Subscription = this.WhenAnyValue(x => x.IsEnabled)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(Glyph1)));
            _glyph2Subscription = this.WhenAnyValue(x => x.IsEnabled)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(Glyph2)));
        }

        private void OnHoverSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(HoverSetting.ScalingFactor)
                or nameof(HoverSetting.HoverLayout))
            {
                WindowScalingFactor = _hoverSettings.ScalingFactor;
                HoverLayout = _hoverSettings.HoverLayout;
            }
        }

        private void OnStatusChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Status.IsPluginReady))
            {
                IsEnabled = _status.IsPluginReady;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _hoverSettings.PropertyChanged -= _hoverSettingsChangedHandler;
            _status.PropertyChanged -= _statusChangedHandler;
            _glyph1Subscription.Dispose();
            _glyph2Subscription.Dispose();
        }

    }
}
