using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Services.IslandCallerService;
using IslandCaller.ViewModels;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace IslandCaller.Views;
public partial class HoverFluent : Window
{
    private const double MinimumMeasuredContentExtent = 16;
    private HoverFluentViewModel vm { get; set; }
    private double scaling { get; set; }
    private bool _isDragging;
    private bool _isApplyingNativeSize;
    private bool _isContentSizeUpdatePending;
    private long _lastPositionLogTime;
    private const int PositionLogIntervalMs = 200;
    private readonly ILogger<HoverFluent> logger = ClassIsland.Shared.IAppHost.GetService<ILogger<HoverFluent>>();
    private readonly WindowTopmostHelper windowTopmostHelper = ClassIsland.Shared.IAppHost.GetService<WindowTopmostHelper>();
    private readonly WindowSizeHelper windowSizeHelper = ClassIsland.Shared.IAppHost.GetService<WindowSizeHelper>();
    private readonly ScreenBrightnessHelper screenBrightnessHelper = ClassIsland.Shared.IAppHost.GetService<ScreenBrightnessHelper>();
    private CancellationTokenSource? topmostCts;

    public HoverFluent()
    {
        InitializeComponent();
        UpdateWindowChrome(Settings.Instance.Hover.HoverLayout);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        vm = DataContext as HoverFluentViewModel;
        scaling = RenderScaling;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdateWindowChrome(vm.HoverLayout);
        HoverControl.SizeChanged += OnHoverContentSizeChanged;
        ScaledContent.SizeChanged += OnHoverContentSizeChanged;
        Dispatcher.UIThread.Post(ApplyNativeWindowSize, DispatcherPriority.Render);
        Position = new PixelPoint((int)Math.Round(vm.PositionX * scaling), (int)Math.Round(vm.PositionY * scaling));
        PositionChanged += OnPositionChanged;
        Activated += OnWindowLayerChanged;
        Deactivated += OnWindowLayerChanged;
        logger.LogDebug($"HoverFluent 坐标: PositionX={(int)Math.Round(vm.PositionX * scaling)}, PositionY={(int)Math.Round(vm.PositionY * scaling)}");
        logger.LogInformation("HoverFluent 悬浮窗初始化成功");

        StartTopmostLoop();
        windowTopmostHelper.EnsureNoActivate(this);
        ApplyTopmost("窗口打开");
    }

    protected override void OnClosed(EventArgs e)
    {
        PositionChanged -= OnPositionChanged;
        if (vm is not null)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        HoverControl.SizeChanged -= OnHoverContentSizeChanged;
        ScaledContent.SizeChanged -= OnHoverContentSizeChanged;
        ScaledContent.LayoutUpdated -= ScaledContent_LayoutUpdated;
        _isContentSizeUpdatePending = false;
        windowSizeHelper.RemoveCompactSizeGuard(this);
        Activated -= OnWindowLayerChanged;
        Deactivated -= OnWindowLayerChanged;
        topmostCts?.Cancel();
        topmostCts?.Dispose();
        topmostCts = null;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HoverFluentViewModel.HoverLayout))
        {
            UpdateWindowChrome(vm.HoverLayout);
        }

        if (e.PropertyName is not (nameof(HoverFluentViewModel.HoverLayout)
            or nameof(HoverFluentViewModel.WindowScalingFactor)))
        {
            return;
        }

        RequestContentSizeUpdate();
    }

    private void OnHoverContentSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RequestContentSizeUpdate();
    }

    public void RequestContentSizeUpdate()
    {
        if (_isContentSizeUpdatePending)
        {
            return;
        }

        _isContentSizeUpdatePending = true;
        ScaledContent.LayoutUpdated += ScaledContent_LayoutUpdated;
    }

    private void ScaledContent_LayoutUpdated(object? sender, EventArgs e)
    {
        ScaledContent.LayoutUpdated -= ScaledContent_LayoutUpdated;
        _isContentSizeUpdatePending = false;
        Dispatcher.UIThread.Post(ApplyNativeWindowSize, DispatcherPriority.Render);
    }

    private void ApplyNativeWindowSize()
    {
        if (vm is null || _isApplyingNativeSize)
        {
            return;
        }

        _isApplyingNativeSize = true;
        try
        {
            if (!TryGetMeasuredContentSize(out var contentSize))
            {
                return;
            }

            windowSizeHelper.UpdateCompactSizeGuard(this, contentSize.Width, contentSize.Height);
            windowSizeHelper.SetWindowSize(this, contentSize.Width, contentSize.Height);
        }
        finally
        {
            _isApplyingNativeSize = false;
        }
    }

    private void UpdateWindowChrome(int hoverLayout)
    {
        bool isMiniLayout = hoverLayout == 2;
        WindowDecorations = isMiniLayout
            ? Avalonia.Controls.WindowDecorations.None
            : Avalonia.Controls.WindowDecorations.BorderOnly;
        TransparencyLevelHint = isMiniLayout
            ? new[] { WindowTransparencyLevel.Transparent }
            : new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };
    }

    private bool TryGetMeasuredContentSize(out Size contentSize)
    {
        // A constrained measure can briefly report a sub-pixel size while
        // changing decorations. Measure the layout's natural transformed size
        // before passing it to the native HWND sizing path.
        ScaledContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        contentSize = ScaledContent.DesiredSize;
        return double.IsFinite(contentSize.Width)
            && double.IsFinite(contentSize.Height)
            && contentSize.Width >= MinimumMeasuredContentExtent
            && contentSize.Height >= MinimumMeasuredContentExtent;
    }

    private void StartTopmostLoop()
    {
        topmostCts?.Cancel();
        topmostCts?.Dispose();
        topmostCts = new CancellationTokenSource();
        var token = topmostCts.Token;

        Task.Run(async () =>
        {
            logger.LogInformation("HoverFluent 置顶任务启动，间隔: 3000ms");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (token.IsCancellationRequested) break;

                await Dispatcher.UIThread.InvokeAsync(() => ApplyTopmost("定时器触发"));
            }
            logger.LogInformation("HoverFluent 置顶任务结束");
        }, token);
    }

    private void OnWindowLayerChanged(object? sender, EventArgs e)
    {
        ApplyTopmost("窗口层级变化");
    }

    private void ApplyTopmost(string reason)
    {
        windowTopmostHelper.EnsureTopmost(this);
        UpdateSecondaryButtonForeground();
        Focusable = false;
        logger.LogTrace("执行窗口置顶，触发原因: {Reason}", reason);
    }

    private void UpdateSecondaryButtonForeground()
    {
        if (!HoverControl.IsSecondaryButtonEffectivelyEnabled)
        {
            HoverControl.ResetSecondaryButtonForeground();
            return;
        }

        var foreground = Colors.Black;
        if (HoverControl.TryGetSecondaryButtonScreenRect(out var buttonRect)
            && screenBrightnessHelper.TryGetAverageRelativeLuminance(buttonRect, out var luminance))
        {
            foreground = ScreenBrightnessHelper.GetRecommendedForeground(luminance);
        }

        HoverControl.SetSecondaryButtonForeground(foreground);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        scaling = RenderScaling;
        if (_isDragging)
        {
            UpdateCompactSizeGuard();
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastPositionLogTime >= PositionLogIntervalMs)
        {
            logger.LogDebug($"窗口位置改变: X={Position.X}, Y={Position.Y}");
            _lastPositionLogTime = now;
        }

        ApplyPositionClampIfNeeded();
    }

    public void BeginDrag()
    {
        _isDragging = true;
        UpdateCompactSizeGuard();
    }

    public void EndDragAndClamp()
    {
        _isDragging = false;
        ApplyPositionClampIfNeeded();
        Dispatcher.UIThread.Post(ApplyNativeWindowSize, DispatcherPriority.Render);
    }

    private void ApplyPositionClampIfNeeded()
    {
        var clamped = ClampPositionToScreenBounds(Position);
        if (clamped != Position)
        {
            Position = clamped;
        }
        UpdateViewModelPosition(clamped.X, clamped.Y);
    }

    private void UpdateCompactSizeGuard()
    {
        if (TryGetMeasuredContentSize(out var contentSize))
        {
            windowSizeHelper.UpdateCompactSizeGuard(this, contentSize.Width, contentSize.Height);
        }
    }

    private PixelPoint ClampPositionToScreenBounds(PixelPoint current)
    {
        var screen = Screens.ScreenFromWindow(this)?.Bounds ?? Screens.Primary.Bounds;
        scaling = RenderScaling;

        int x = current.X;
        int y = current.Y;
        int w = (int)Math.Round(Bounds.Width * scaling);
        int h = (int)Math.Round(Bounds.Height * scaling);

        if (x < screen.X) x = screen.X;
        if (y < screen.Y) y = screen.Y;
        if (x + w > screen.X + screen.Width)
        {
            x = screen.X + screen.Width - w;
            logger.LogInformation("调整X坐标以适应屏幕");
        }
        if (y + h > screen.Y + screen.Height)
        {
            y = screen.Y + screen.Height - h;
            logger.LogInformation("调整Y坐标以适应屏幕");
        }

        return new PixelPoint(x, y);
    }

    private void UpdateViewModelPosition(int x, int y)
    {
        vm.PositionX = x / scaling;
        vm.PositionY = y / scaling;
    }
}

