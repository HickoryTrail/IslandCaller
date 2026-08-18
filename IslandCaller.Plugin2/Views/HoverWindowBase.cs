using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.ViewModels;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace IslandCaller.Views;

/// <summary>
/// Hosts the theme-independent hover window lifecycle, sizing and topmost behavior.
/// </summary>
public abstract class HoverWindowBase : Window, IHoverWindow
{
    private const double MinimumMeasuredContentExtent = 16;
    private readonly ILogger<HoverWindowBase> _logger = IAppHost.GetService<ILogger<HoverWindowBase>>();
    private readonly WindowTopmostHelper _windowTopmostHelper = IAppHost.GetService<WindowTopmostHelper>();
    private readonly WindowSizeHelper _windowSizeHelper = IAppHost.GetService<WindowSizeHelper>();
    private HoverFluentViewModel? _viewModel;
    private Control? _hoverControl;
    private Control? _measuredContent;
    private double _scaling;
    private bool _isDragging;
    private bool _isApplyingNativeSize;
    private bool _isContentSizeUpdatePending;
    private long _lastPositionLogTime;
    private CancellationTokenSource? _topmostCts;

    protected void InitializeHoverWindow(Control hoverControl, Control measuredContent)
    {
        _hoverControl = hoverControl;
        _measuredContent = measuredContent;
        UpdateWindowChrome(Settings.Instance.Hover.HoverLayout);
    }

    protected virtual void UpdateWindowChrome(int hoverLayout)
    {
        bool isMiniLayout = hoverLayout == 2;
        WindowDecorations = isMiniLayout
            ? Avalonia.Controls.WindowDecorations.None
            : Avalonia.Controls.WindowDecorations.BorderOnly;
        TransparencyLevelHint = isMiniLayout
            ? [Avalonia.Controls.WindowTransparencyLevel.Transparent]
            : [Avalonia.Controls.WindowTransparencyLevel.AcrylicBlur, Avalonia.Controls.WindowTransparencyLevel.Transparent];
    }

    protected virtual void ApplyThemeTopmost()
    {
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (_hoverControl is null || _measuredContent is null)
        {
            throw new InvalidOperationException("悬浮窗尚未初始化内容控件。");
        }

        _viewModel = DataContext as HoverFluentViewModel
            ?? throw new InvalidOperationException("悬浮窗缺少 HoverFluentViewModel 数据上下文。");
        _scaling = RenderScaling;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateWindowChrome(_viewModel.HoverLayout);
        _hoverControl.SizeChanged += OnHoverContentSizeChanged;
        _measuredContent.SizeChanged += OnHoverContentSizeChanged;
        Dispatcher.UIThread.Post(ApplyNativeWindowSize, DispatcherPriority.Render);
        Position = new PixelPoint(
            (int)Math.Round(_viewModel.PositionX * _scaling),
            (int)Math.Round(_viewModel.PositionY * _scaling));
        PositionChanged += OnPositionChanged;
        Activated += OnWindowLayerChanged;
        Deactivated += OnWindowLayerChanged;

        _logger.LogInformation("悬浮窗初始化成功：{Theme}", GetType().Name);
        StartTopmostLoop();
        _windowTopmostHelper.EnsureNoActivate(this);
        ApplyTopmost("窗口打开");
    }

    protected override void OnClosed(EventArgs e)
    {
        PositionChanged -= OnPositionChanged;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_hoverControl is not null)
        {
            _hoverControl.SizeChanged -= OnHoverContentSizeChanged;
        }

        if (_measuredContent is not null)
        {
            _measuredContent.SizeChanged -= OnHoverContentSizeChanged;
            _measuredContent.LayoutUpdated -= MeasuredContentOnLayoutUpdated;
        }

        _isContentSizeUpdatePending = false;
        _windowSizeHelper.RemoveCompactSizeGuard(this);
        Activated -= OnWindowLayerChanged;
        Deactivated -= OnWindowLayerChanged;
        _topmostCts?.Cancel();
        _topmostCts?.Dispose();
        _topmostCts = null;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(HoverFluentViewModel.HoverLayout))
        {
            UpdateWindowChrome(_viewModel.HoverLayout);
        }

        if (e.PropertyName is nameof(HoverFluentViewModel.HoverLayout)
            or nameof(HoverFluentViewModel.WindowScalingFactor))
        {
            RequestContentSizeUpdate();
        }
    }

    private void OnHoverContentSizeChanged(object? sender, SizeChangedEventArgs e) => RequestContentSizeUpdate();

    public void RequestContentSizeUpdate()
    {
        if (_measuredContent is null || _isContentSizeUpdatePending)
        {
            return;
        }

        _isContentSizeUpdatePending = true;
        _measuredContent.LayoutUpdated += MeasuredContentOnLayoutUpdated;
    }

    private void MeasuredContentOnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_measuredContent is null)
        {
            return;
        }

        _measuredContent.LayoutUpdated -= MeasuredContentOnLayoutUpdated;
        _isContentSizeUpdatePending = false;
        Dispatcher.UIThread.Post(ApplyNativeWindowSize, DispatcherPriority.Render);
    }

    private void ApplyNativeWindowSize()
    {
        if (_measuredContent is null || _isApplyingNativeSize || !TryGetMeasuredContentSize(out var contentSize))
        {
            return;
        }

        _isApplyingNativeSize = true;
        try
        {
            _windowSizeHelper.UpdateCompactSizeGuard(this, contentSize.Width, contentSize.Height);
            _windowSizeHelper.SetWindowSize(this, contentSize.Width, contentSize.Height);
        }
        finally
        {
            _isApplyingNativeSize = false;
        }
    }

    private bool TryGetMeasuredContentSize(out Size contentSize)
    {
        contentSize = default;
        if (_measuredContent is null)
        {
            return false;
        }

        _measuredContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        contentSize = _measuredContent.DesiredSize;
        return double.IsFinite(contentSize.Width)
            && double.IsFinite(contentSize.Height)
            && contentSize.Width >= MinimumMeasuredContentExtent
            && contentSize.Height >= MinimumMeasuredContentExtent;
    }

    private void StartTopmostLoop()
    {
        _topmostCts?.Cancel();
        _topmostCts?.Dispose();
        _topmostCts = new CancellationTokenSource();
        var token = _topmostCts.Token;

        Task.Run(async () =>
        {
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

                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => ApplyTopmost("定时器触发"));
                }
            }
        }, token);
    }

    private void OnWindowLayerChanged(object? sender, EventArgs e) => ApplyTopmost("窗口层级变化");

    private void ApplyTopmost(string reason)
    {
        _windowTopmostHelper.EnsureTopmost(this);
        ApplyThemeTopmost();
        Focusable = false;
        _logger.LogTrace("执行悬浮窗置顶，触发原因: {Reason}", reason);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        _scaling = RenderScaling;
        if (_isDragging)
        {
            UpdateCompactSizeGuard();
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastPositionLogTime >= 200)
        {
            _lastPositionLogTime = now;
            _logger.LogDebug("悬浮窗位置改变: X={X}, Y={Y}", Position.X, Position.Y);
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

        if (_viewModel is not null && _scaling > 0)
        {
            _viewModel.PositionX = clamped.X / _scaling;
            _viewModel.PositionY = clamped.Y / _scaling;
        }
    }

    private void UpdateCompactSizeGuard()
    {
        if (TryGetMeasuredContentSize(out var contentSize))
        {
            _windowSizeHelper.UpdateCompactSizeGuard(this, contentSize.Width, contentSize.Height);
        }
    }

    private PixelPoint ClampPositionToScreenBounds(PixelPoint current)
    {
        var screen = Screens.ScreenFromWindow(this)?.Bounds
            ?? Screens.Primary?.Bounds
            ?? new PixelRect(0, 0, 1920, 1080);
        _scaling = RenderScaling;
        int width = (int)Math.Round(Bounds.Width * _scaling);
        int height = (int)Math.Round(Bounds.Height * _scaling);
        int x = Math.Clamp(current.X, screen.X, Math.Max(screen.X, screen.Right - width));
        int y = Math.Clamp(current.Y, screen.Y, Math.Max(screen.Y, screen.Bottom - height));
        return new PixelPoint(x, y);
    }
}
