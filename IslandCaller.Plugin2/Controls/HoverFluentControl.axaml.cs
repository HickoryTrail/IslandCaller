using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using IslandCaller.Services.IslandCallerService;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Controls;

public partial class HoverFluentControl : UserControl
{
    private const int TouchDragIgnoreDurationMs = 75;
    private const int ClickMoveThresholdPx = 10;
    private const int TouchMoveDeadZonePx = 2;

    private IslandCallerService IslandCallerService { get; }
    private Window parentwindow { get; set; }
    private ILogger<HoverFluentControl> logger { get; set; }
    public PixelPoint lastWindowPosition { get; set; }
    private WindowDragHelper windowDragHelper { get; set; }
    private long _lastDragTime;
    private bool _isManualDragging;
    private IPointer? _dragPointer;
    private PixelPoint _dragStartWindowPosition;
    private Point _dragStartPointerPosition;
    private PixelPoint _dragStartPointerScreenPosition;
    private PixelPoint _lastAcceptedPointerScreenPosition;
    private DragClickAction _pendingClickAction = DragClickAction.None;
    private long _manualDragStartTime;
    private bool _touchDragDelayElapsed;
    private bool _touchDragThresholdElapsed;

    public HoverFluentControl()
    {
        IslandCallerService = IAppHost.GetService<IslandCallerService>();
        logger = IAppHost.GetService<ILogger<HoverFluentControl>>();
        windowDragHelper = IAppHost.GetService<WindowDragHelper>();
        InitializeComponent();
        DragSurface.AddHandler(InputElement.PointerPressedEvent, DragSurface_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        // 移动事件只处理一次，避免窗口移动后同一触控采样在冒泡阶段再次计算而形成反馈抖动。
        DragSurface.AddHandler(InputElement.PointerMovedEvent, DragPointerMoved, RoutingStrategies.Tunnel, true);
        DragSurface.AddHandler(InputElement.PointerReleasedEvent, DragPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        DragSurface.AddHandler(InputElement.PointerCaptureLostEvent, DragPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    private async void DragSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (Environment.TickCount64 - _lastDragTime < 50)
        {
            logger.LogDebug("捕获到重复的输入");
            _lastDragTime = Environment.TickCount64;
            return;
        }

        var clickAction = GetClickAction(e);
        logger.LogDebug("DragSurface_PointerPressed: 触发窗口拖动，点击动作: {ClickAction}", clickAction);
        _lastDragTime = Environment.TickCount64;

        // 触发窗口拖动
        parentwindow = this.VisualRoot as Window;
        if (parentwindow == null)
        {
            logger.LogWarning("DragSurface_PointerPressed: 无法获取窗口句柄，跳过拖动。");
            return;
        }

        var hoverWindow = parentwindow as HoverFluent;
        lastWindowPosition = parentwindow.Position;
        if (TryStartManualDrag(parentwindow, e, clickAction, DragSurface))
        {
            return;
        }

        hoverWindow?.BeginDrag();
        await windowDragHelper.DragMoveAsync(parentwindow, e.Pointer.Type);
        logger.LogDebug("DragSurface_PointerPressed: 窗口拖动结束");
        hoverWindow?.EndDragAndClamp();

        if (clickAction != DragClickAction.None && IsWithinClickThreshold(parentwindow.Position, lastWindowPosition))
        {
            logger.LogDebug("DragSurface_PointerPressed: 窗口位置未变化，触发点击事件");
            await InvokeClickActionAsync(clickAction, parentwindow);
        }
    }

    private DragClickAction GetClickAction(PointerPressedEventArgs e)
    {
        if (Button1.IsEnabled && IsPointerWithin(Button1, e))
        {
            return DragClickAction.Button1;
        }

        if (Button2.IsEnabled && IsPointerWithin(Button2, e))
        {
            return DragClickAction.Button2;
        }

        return DragClickAction.None;
    }

    private static bool IsPointerWithin(Control control, PointerEventArgs e)
    {
        var position = e.GetPosition(control);
        return position.X >= 0
            && position.Y >= 0
            && position.X <= control.Bounds.Width
            && position.Y <= control.Bounds.Height;
    }

    private async Task InvokeClickActionAsync(DragClickAction clickAction, Window owner)
    {
        if (clickAction == DragClickAction.Button1)
        {
            IslandCallerService.ShowRandomStudent(1);
            return;
        }

        if (clickAction == DragClickAction.Button2)
        {
            await new PersonalCall().ShowOwnedNoActivateAsync(owner);
        }
    }

    private bool TryStartManualDrag(Window window, PointerPressedEventArgs e, DragClickAction clickAction, IInputElement? captureTarget)
    {
        if (e.Pointer.Type != PointerType.Touch && e.Pointer.Type != PointerType.Pen)
        {
            return false;
        }

        _isManualDragging = true;
        _dragPointer = e.Pointer;
        _manualDragStartTime = Environment.TickCount64;
        _touchDragDelayElapsed = false;
        _touchDragThresholdElapsed = false;
        ResetManualDragAnchor(window, e.GetPosition(window));
        _pendingClickAction = clickAction;

        if (window is HoverFluent hoverWindow)
        {
            hoverWindow.BeginDrag();
        }

        e.Pointer.Capture(captureTarget ?? this);
        logger.LogDebug("开始手动拖动，PointerType: {PointerType}", e.Pointer.Type);
        return true;
    }

    private void DragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isManualDragging || _dragPointer == null || e.Pointer != _dragPointer || parentwindow == null)
        {
            return;
        }
        var current = e.GetPosition(parentwindow);

        if (e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen)
        {
            var elapsed = Environment.TickCount64 - _manualDragStartTime;
            if (!_touchDragDelayElapsed)
            {
                if (elapsed < TouchDragIgnoreDurationMs)
                {
                    return;
                }

                _touchDragDelayElapsed = true;
                ResetManualDragAnchor(parentwindow, current);
                logger.LogDebug("触控拖动已超过忽略时间，开始监测拖动阈值");
                return;
            }

            if (!_touchDragThresholdElapsed)
            {
                var thresholdDeltaX = current.X - _dragStartPointerPosition.X;
                var thresholdDeltaY = current.Y - _dragStartPointerPosition.Y;
                if (Math.Abs(thresholdDeltaX) < ClickMoveThresholdPx && Math.Abs(thresholdDeltaY) < ClickMoveThresholdPx)
                {
                    return;
                }

                _touchDragThresholdElapsed = true;
                ResetManualDragAnchor(parentwindow, current);
                logger.LogDebug("触控拖动已超过点击阈值，开始移动窗口");
                return;
            }
        }

        var currentScreen = parentwindow.PointToScreen(current);
        if (IsWithinTouchMoveDeadZone(currentScreen, _lastAcceptedPointerScreenPosition))
        {
            return;
        }

        var deltaX = currentScreen.X - _dragStartPointerScreenPosition.X;
        var deltaY = currentScreen.Y - _dragStartPointerScreenPosition.Y;
        var newPosition = new PixelPoint(
            _dragStartWindowPosition.X + deltaX,
            _dragStartWindowPosition.Y + deltaY);

        if (parentwindow.Position != newPosition)
        {
            if (windowDragHelper.SetWindowPosition(parentwindow, newPosition))
            {
                _lastAcceptedPointerScreenPosition = currentScreen;
            }
        }
    }

    private async void DragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        await EndManualDragAsync(e.Pointer);
    }

    private async void DragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        await EndManualDragAsync(e.Pointer);
    }

    private async Task EndManualDragAsync(IPointer pointer)
    {
        if (!_isManualDragging || _dragPointer == null || pointer != _dragPointer)
        {
            return;
        }

        _isManualDragging = false;
        _dragPointer = null;
        pointer.Capture(null);

        if (parentwindow is HoverFluent hoverWindow)
        {
            hoverWindow.EndDragAndClamp();
        }

        if (parentwindow != null && _pendingClickAction != DragClickAction.None && IsWithinClickThreshold(parentwindow.Position, lastWindowPosition))
        {
            logger.LogDebug("手动拖动未改变窗口位置，触发点击事件");
            await InvokeClickActionAsync(_pendingClickAction, parentwindow);
        }

        _pendingClickAction = DragClickAction.None;
        _touchDragDelayElapsed = false;
        _touchDragThresholdElapsed = false;
    }

    private void ResetManualDragAnchor(Window window, Point pointerPosition)
    {
        _dragStartPointerPosition = pointerPosition;
        _dragStartPointerScreenPosition = window.PointToScreen(pointerPosition);
        _lastAcceptedPointerScreenPosition = _dragStartPointerScreenPosition;
        _dragStartWindowPosition = window.Position;
    }

    private static bool IsWithinTouchMoveDeadZone(PixelPoint currentPosition, PixelPoint acceptedPosition)
    {
        var deltaX = currentPosition.X - acceptedPosition.X;
        var deltaY = currentPosition.Y - acceptedPosition.Y;
        return deltaX * deltaX + deltaY * deltaY <= TouchMoveDeadZonePx * TouchMoveDeadZonePx;
    }

    private static bool IsWithinClickThreshold(PixelPoint currentPosition, PixelPoint originalPosition)
    {
        return currentPosition == originalPosition
            || (Math.Abs(currentPosition.X - originalPosition.X) < ClickMoveThresholdPx
                && Math.Abs(currentPosition.Y - originalPosition.Y) < ClickMoveThresholdPx);
    }

    private enum DragClickAction
    {
        None,
        Button1,
        Button2
    }
}

