using Avalonia.Media;
using ClassIsland.Shared;
using IslandCaller.Helpers;
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
        private readonly AppearanceSetting _appearanceSettings;
        private readonly PropertyChangedEventHandler _hoverSettingsChangedHandler;
        private readonly PropertyChangedEventHandler _statusChangedHandler;
        private readonly PropertyChangedEventHandler _appearanceChangedHandler;
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

        // 个性化外观
        private string _hoverText = "Call";
        public string HoverText
        {
            get => _hoverText;
            set => this.RaiseAndSetIfChanged(ref _hoverText, value);
        }

        private string _hoverImagePath = string.Empty;
        public string HoverImagePath
        {
            get => _hoverImagePath;
            set
            {
                this.RaiseAndSetIfChanged(ref _hoverImagePath, value);
                this.RaisePropertyChanged(nameof(HasHoverImage));
                LoadHoverImage();
            }
        }

        /// <summary>是否已选择抽选时图片（有图片时用图片代替默认图标，文字照常显示）。</summary>
        public bool HasHoverImage => !string.IsNullOrWhiteSpace(_hoverImagePath);

        private IImage? _hoverImageSource;
        public IImage? HoverImageSource
        {
            get => _hoverImageSource;
            private set => this.RaiseAndSetIfChanged(ref _hoverImageSource, value);
        }

        private IBrush? _accentBrush;
        public IBrush? AccentBrush
        {
            get => _accentBrush;
            private set => this.RaiseAndSetIfChanged(ref _accentBrush, value);
        }

        private FontFamily? _fontFamily;
        public FontFamily? FontFamily
        {
            get => _fontFamily;
            private set => this.RaiseAndSetIfChanged(ref _fontFamily, value);
        }

        private void LoadHoverImage()
        {
            // 支持内置图片（builtin:xxx）与本地路径
            HoverImageSource = BuiltinImages.Load(_hoverImagePath);
        }

        private void LoadAppearance()
        {
            var appearance = Settings.Instance.Appearance;
            HoverText = appearance.HoverText;
            HoverImagePath = appearance.HoverImagePath;
            AccentBrush = ParseBrush(appearance.AccentColor);
            FontFamily = string.IsNullOrWhiteSpace(appearance.FontFamily)
                ? null
                : new FontFamily(appearance.FontFamily);
        }

        private static IBrush? ParseBrush(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            try
            {
                return new SolidColorBrush(Color.Parse(hex));
            }
            catch
            {
                return null;
            }
        }

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
            LoadAppearance();

            // 监听外观设置变化
            _appearanceSettings = Settings.Instance.Appearance;
            _appearanceChangedHandler = (_, _) => LoadAppearance();
            _appearanceSettings.PropertyChanged += _appearanceChangedHandler;

            // 监听设置变化
            _hoverSettingsChangedHandler = OnHoverSettingsChanged;
            _hoverSettings.PropertyChanged += _hoverSettingsChangedHandler;
            _status = IAppHost.GetService<Status>();
            IsEnabled = _status.IsPluginReady;
            _statusChangedHandler = OnStatusChanged;
            _status.PropertyChanged += _statusChangedHandler;
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
            _appearanceSettings.PropertyChanged -= _appearanceChangedHandler;
        }

    }
}
