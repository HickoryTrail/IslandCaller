using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClassIsland.Core.Controls;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

internal class WindowsManager
{
    private readonly ILogger<WindowsManager> _logger;
    private readonly ScreenBrightnessHelper _screenBrightnessHelper;
    private readonly LiquidGlassRuntime _liquidGlassRuntime;
    private bool _isInitialized;
    private bool _isRecreatingHover;

    /// <summary>当前活动的结果展示任务控制源。最新一次点名会取消上一个，保证同一时刻只有一个任务控制展示窗口。</summary>
    private CancellationTokenSource? _activeShowCts;

    public Window? HoverWindow { get; private set; }
    public Window? ShowerWindow { get; private set; }

    public WindowsManager(
        ILogger<WindowsManager> logger,
        ScreenBrightnessHelper screenBrightnessHelper,
        LiquidGlassRuntime liquidGlassRuntime)
    {
        _logger = logger;
        _screenBrightnessHelper = screenBrightnessHelper;
        _liquidGlassRuntime = liquidGlassRuntime;
        _logger.LogTrace("WindowsManager created.");
    }

    internal void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        Settings.Instance.Hover.PropertyChanged += OnHoverSettingChanged;
        if (Settings.Instance.Hover.IsEnable)
        {
            ShowHoverWindow();
        }

        _logger.LogInformation("WindowsManager initialized.");
    }

    internal async Task ShowCallWindowAsync(string text, float duration, CancellationToken token)
    {
        // —— 最新优先：取消上一个仍在展示/等待的任务，避免连续点名时多个任务
        //    交错 Show/Hide 同一个窗口，导致窗口残留关不掉或 UI 卡死。 ——
        try
        {
            _activeShowCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 上一个任务已完成清理，忽略
        }

        try
        {
            _activeShowCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        _activeShowCts = null;

        var myCts = new CancellationTokenSource();
        _activeShowCts = myCts;
        try
        {
            // 本次点名在开始前已被打断且没有后继：收起可能残留的窗口，不展示。
            if (token.IsCancellationRequested)
            {
                ShowerWindow?.Hide();
                return;
            }

            using var _ = token.Register(myCts.Cancel);

            _logger.LogInformation("Showing call window for {Duration} seconds with text: {Text}", duration, text);
            var appearance = Settings.Instance.Appearance;
            var icon = CreateResultIcon(appearance);
            var nameText = new TextBlock
            {
                Text = text,
                FontSize = appearance.ResultFontSize,
                FontWeight = FontWeight.Bold,
                FontStretch = FontStretch.Expanded,
                FontFamily = string.IsNullOrWhiteSpace(appearance.FontFamily) ? null : new FontFamily(appearance.FontFamily),
                Margin = new Thickness(15, 0, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var showPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(25, 0),
                Children = { icon, nameText }
            };

            // 复用展示窗口：液态玻璃窗口反复创建/销毁会导致 MorerialsAvalonia
            // 的 Desktop Duplication / D3D11 资源无法及时释放，内存持续增长。
            // 因此只创建一次，点名时更新内容并显示，结束后隐藏而非销毁。
            var showerWindow = GetOrCreateShowerWindow();

            if (showerWindow is LiquidShower liquidShower)
            {
                liquidShower.SetDisplayContent(showPanel);
            }
            else if (showerWindow.Content is Border fluentHost)
            {
                // 复用 Fluent 展示窗口时，仅替换内部内容，保留外层圆角 Border。
                fluentHost.Child = showPanel;
            }
            else
            {
                // Fluent 展示窗口内容包一层圆角 Border，让「结果背景色」与「界面圆角」生效。
                showerWindow.Content = new Border
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    Child = showPanel
                };
                showerWindow.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                showerWindow.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            }

            showPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var screen = showerWindow.Screens.Primary;
            PixelRect? captureRect = null;
            if (screen is not null && showPanel.DesiredSize.Width > 0)
            {
                var scaling = screen.Scaling;
                var width = Math.Max(1, (int)Math.Ceiling(showPanel.DesiredSize.Width));
                // 高度随结果字号自适应，避免大字号时被固定 110 高度裁剪
                var height = Math.Max(110, (int)Math.Ceiling(showPanel.DesiredSize.Height + 40));
                showerWindow.Width = width;
                showerWindow.Height = height;

                if (showerWindow is LiquidShower liquidGlassWindow)
                {
                    liquidGlassWindow.ApplyGlassExtent(height, appearance.CornerRadius);
                }

                var widthPixels = Math.Max(1, (int)Math.Ceiling(width * scaling));
                var heightPixels = Math.Max(1, (int)Math.Ceiling(height * scaling));
                var workArea = screen.WorkingArea;
                var x = workArea.X + Math.Max(0, (workArea.Width - widthPixels) / 2);
                var y = workArea.Y + Math.Max(0, (workArea.Height - heightPixels) / 2);
                showerWindow.Position = new PixelPoint(x, y);
                captureRect = new PixelRect(x, y, widthPixels, heightPixels);
            }

            if (showerWindow is FluentShower)
            {
                // 自定义结果文字色优先；留空则按屏幕亮度自动选黑白。
                // 自动黑白时把截屏亮度计算放到后台线程，避免同步 BitBlt 阻塞 UI 导致卡顿。
                var foreground = ParseBrush(appearance.ResultTextColor);
                if (foreground is null && captureRect is PixelRect rect)
                {
                    showerWindow.Show();
                    IBrush? recommended = null;
                    try
                    {
                        recommended = await Task.Run(() =>
                        {
                            return _screenBrightnessHelper.TryGetAverageRelativeLuminance(rect, out var luminance)
                                ? (ScreenBrightnessHelper.GetRecommendedForeground(luminance) == Colors.White
                                    ? Brushes.White
                                    : Brushes.Black)
                                : null;
                        });
                    }
                    catch
                    {
                        // 截图失败则保持默认前景色
                    }

                    if (recommended is not null && !myCts.IsCancellationRequested && showerWindow.IsVisible)
                    {
                        foreground = recommended;
                    }
                }
                else
                {
                    showerWindow.Show();
                }

                if (foreground is not null)
                {
                    // 图标是 Path（几何图标）时用 Fill，图片则保持原样
                    if (icon is Avalonia.Controls.Shapes.Path pathIcon)
                    {
                        pathIcon.Fill = foreground;
                    }

                    nameText.Foreground = foreground;
                }

                // 自定义结果窗口背景色 + 界面圆角（作用于外层圆角 Border）
                var background = ParseBrush(appearance.ResultBackground);
                if (showerWindow.Content is Border host)
                {
                    host.Background = background;
                    var windowHeight = double.IsFinite(showerWindow.Height) ? showerWindow.Height : 110;
                    host.CornerRadius = new CornerRadius(
                        Math.Min(Math.Max(0, appearance.CornerRadius), windowHeight / 2));
                }
            }
            else
            {
                showerWindow.Show();
            }

            try
            {
                await Task.Delay((int)(duration * 1000), myCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            // 隐藏而非关闭，保留 MaterialHost 的 GPU 管线供下次点名复用。
            // 仅当自己仍是最新的展示任务时才隐藏，避免把后续点名刚显示的窗口误隐藏。
            if (_activeShowCts == myCts)
            {
                _activeShowCts = null;
                ShowerWindow?.Hide();
            }

            myCts.Dispose();
        }

        _logger.LogInformation("Call window hidden for text: {Text}", text);
    }

    internal void ShowCallWindow(string text, float duration, CancellationToken token) => _ = ShowCallWindowAsync(text, duration, token);

    internal void ShowHoverWindow()
    {
        HoverWindow ??= CreateHoverWindow();
        HoverWindow.Show();
        _logger.LogInformation("Hover window shown: {Theme}", HoverWindow.GetType().Name);
    }

    internal void HideHoverWindow()
    {
        HoverWindow?.Hide();
        _logger.LogInformation("Hover window hidden.");
    }

    internal void CloseHoverWindow()
    {
        HoverWindow?.Close();
        HoverWindow = null;
        _logger.LogInformation("Hover window closed.");
    }

    private Window CreateHoverWindow() => _liquidGlassRuntime.CanUseHoverTheme()
        ? new HoverLiquid()
        : new HoverFluent();

    private Window CreateShowerWindow() => _liquidGlassRuntime.CanUseShowerTheme()
        ? new LiquidShower()
        : new FluentShower();

    /// <summary>
    /// 获取可复用的展示窗口，主题切换时才重建，避免反复创建/销毁
    /// 液态玻璃窗口导致 GPU 资源泄漏。
    /// </summary>
    private Window GetOrCreateShowerWindow()
    {
        bool useLiquid = _liquidGlassRuntime.CanUseShowerTheme();
        bool currentIsLiquid = ShowerWindow is LiquidShower;

        if (ShowerWindow is not null && currentIsLiquid == useLiquid)
        {
            return ShowerWindow;
        }

        // 主题变化或首次创建：关闭旧窗口并重建
        ShowerWindow?.Close();
        ShowerWindow = null;

        var window = CreateShowerWindow();
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.SizeToContent = SizeToContent.Manual;
        // 点击结果窗口任意位置立即关闭本次展示（用户不想等展示时长结束）。
        window.PointerPressed += (_, _) => _activeShowCts?.Cancel();
        ShowerWindow = window;
        _logger.LogInformation("展示窗口已创建：{Theme}", window.GetType().Name);
        return window;
    }

    private void OnHoverSettingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HoverSetting.HoverTheme) || _isRecreatingHover || HoverWindow is null)
        {
            return;
        }

        _isRecreatingHover = true;
        try
        {
            bool wasVisible = HoverWindow.IsVisible;
            CloseHoverWindow();
            if (wasVisible)
            {
                ShowHoverWindow();
            }
        }
        finally
        {
            _isRecreatingHover = false;
        }
    }

    /// <summary>结果窗口名字前的图标（洗牌/随机）。</summary>
    private const string CallGlyphGeometry =
        "M10.59 9.17L5.41 4 4 5.41l5.17 5.17 1.42-1.41zM14.5 4l2.04 2.04L4 18.59 5.41 20 17.96 7.46 20 9.5V4h-5.5zm.33 9.41l-1.41 1.41 3.13 3.13L14.5 20H20v-5.5l-2.04 2.04-3.13-3.13z";

    /// <summary>根据外观设置创建结果窗口名字前的图标（内置/本地图片优先，否则用几何图标）。</summary>
    private static Control CreateResultIcon(AppearanceSetting appearance)
    {
        var size = appearance.ResultFontSize;

        var imageSource = BuiltinImages.Load(appearance.ResultImagePath);
        if (imageSource is not null)
        {
            return new Image
            {
                Source = imageSource,
                Width = size,
                Height = size,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        }

        return new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(CallGlyphGeometry),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
    }

    private static IBrush? ParseBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        return Color.TryParse(hex, out var color) ? new SolidColorBrush(color) : null;
    }
}
