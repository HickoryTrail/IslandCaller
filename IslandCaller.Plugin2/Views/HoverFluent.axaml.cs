using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using IslandCaller.Helpers;
using IslandCaller.Services.IslandCallerService;
using IslandCaller.ViewModels;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Views;
public partial class HoverFluent : Window
{
    private HoverFluentViewModel vm { get; set; }
    private double scaling { get; set; }
    private bool _isDragging;
    private long _lastPositionLogTime;
    private const int PositionLogIntervalMs = 200;
    private readonly ILogger<HoverFluent> logger = ClassIsland.Shared.IAppHost.GetService<ILogger<HoverFluent>>();
    private readonly IslandCallerService IslandCallerService = ClassIsland.Shared.IAppHost.GetService<IslandCallerService>();
    private readonly WindowTopmostHelper windowTopmostHelper = ClassIsland.Shared.IAppHost.GetService<WindowTopmostHelper>();
    private readonly WindowDragHelper windowDragHelper = ClassIsland.Shared.IAppHost.GetService<WindowDragHelper>();
    private CancellationTokenSource? topmostCts;

    public HoverFluent()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        vm = DataContext as HoverFluentViewModel;
        scaling = RenderScaling;
        Position = new PixelPoint((int)Math.Round(vm.PositionX * scaling), (int)Math.Round(vm.PositionY * scaling));
        PositionChanged += OnPositionChanged;
        Activated += OnWindowLayerChanged;
        Deactivated += OnWindowLayerChanged;
        logger.LogDebug($"HoverFluent 坐标: PositionX={(int)Math.Round(vm.PositionX * scaling)}, PositionY={(int)Math.Round(vm.PositionY * scaling)}");
        logger.LogInformation("HoverFluent 悬浮窗初始化成功");

        var platformHandle = TryGetPlatformHandle();
        if (platformHandle == null)
        {
            logger.LogWarning("无法获取窗口句柄，取消触控输入注册失败。");
        }
        else
        {
            windowDragHelper.EnsureTouchInputDisabled(platformHandle.Handle);
        }

        StartTopmostLoop();
        windowTopmostHelper.EnsureNoActivate(this);
        ApplyTopmost("窗口打开");
    }

    protected override void OnClosed(EventArgs e)
    {
        PositionChanged -= OnPositionChanged;
        Activated -= OnWindowLayerChanged;
        Deactivated -= OnWindowLayerChanged;
        topmostCts?.Cancel();
        topmostCts?.Dispose();
        topmostCts = null;
        base.OnClosed(e);
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
        Focusable = false;
        logger.LogTrace("执行窗口置顶，触发原因: {Reason}", reason);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        scaling = RenderScaling;
        if (_isDragging)
        {
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
    }

    public void EndDragAndClamp()
    {
        _isDragging = false;
        ApplyPositionClampIfNeeded();
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

