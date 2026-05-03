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
        Button1.AddHandler(InputElement.PointerPressedEvent, Button1_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        Button2.AddHandler(InputElement.PointerPressedEvent, Button2_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        Button1.AddHandler(InputElement.PointerMovedEvent, DragPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        Button2.AddHandler(InputElement.PointerMovedEvent, DragPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        Button1.AddHandler(InputElement.PointerReleasedEvent, DragPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        Button2.AddHandler(InputElement.PointerReleasedEvent, DragPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        Button1.AddHandler(InputElement.PointerCaptureLostEvent, DragPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        Button2.AddHandler(InputElement.PointerCaptureLostEvent, DragPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }
    private async void Button1_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
        if(Environment.TickCount64 - _lastDragTime < 50)
        {
            logger.LogDebug("捕获到重复的输入");
            _lastDragTime = Environment.TickCount64;
            return;
        }
        logger.LogDebug("Button1_PointerPressed: 触发窗口拖动");
        _lastDragTime = Environment.TickCount64;
        // 触发窗口拖动
        parentwindow = this.GetVisualRoot() as Window;
        if (parentwindow == null)
        {
            logger.LogWarning("Button1_PointerPressed: 无法获取窗口句柄，跳过拖动。");
            return;
        }
        var hoverWindow = parentwindow as HoverFluent;
        lastWindowPosition = parentwindow.Position;
        if (TryStartManualDrag(parentwindow, e, DragClickAction.Button1, sender as IInputElement))
        {
            return;
        }
        hoverWindow?.BeginDrag();
        await windowDragHelper.DragMoveAsync(parentwindow, e.Pointer.Type);
        logger.LogDebug("Button1_PointerPressed: 窗口拖动结束");
        hoverWindow?.EndDragAndClamp();
        if (IsWithinClickThreshold(parentwindow.Position, lastWindowPosition))
        {
            logger.LogDebug("Button1_PointerPressed: 窗口位置未变化，触发点击事件");
            IslandCallerService.ShowRandomStudent(1);
        }
    }
    private async void Button2_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (Environment.TickCount64 - _lastDragTime < 50)
        {
            logger.LogDebug("捕获到重复的输入");
            _lastDragTime = Environment.TickCount64;
            return;
        }
        logger.LogDebug("Button2_PointerPressed: 触发窗口拖动");
        _lastDragTime = Environment.TickCount64;
        // 触发窗口拖动
        parentwindow = this.GetVisualRoot() as Window;
        if (parentwindow == null)
        {
            logger.LogWarning("Button2_PointerPressed: 无法获取窗口句柄，跳过拖动。");
            return;
        }
        var hoverWindow = parentwindow as HoverFluent;
        lastWindowPosition = parentwindow.Position;
        if (TryStartManualDrag(parentwindow, e, DragClickAction.Button2, sender as IInputElement))
        {
            return;
        }
        hoverWindow?.BeginDrag();
        await windowDragHelper.DragMoveAsync(parentwindow, e.Pointer.Type);
        logger.LogDebug("Button2_PointerPressed: 窗口拖动结束");
        hoverWindow?.EndDragAndClamp();
        if (IsWithinClickThreshold(parentwindow.Position, lastWindowPosition))
        {
            logger.LogDebug("Button2_PointerPressed: 窗口位置未变化，触发点击事件");
            await new PersonalCall().ShowOwnedNoActivateAsync(parentwindow);
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
        var deltaX = currentScreen.X - _dragStartPointerScreenPosition.X;
        var deltaY = currentScreen.Y - _dragStartPointerScreenPosition.Y;
        var newPosition = new PixelPoint(
            _dragStartWindowPosition.X + deltaX,
            _dragStartWindowPosition.Y + deltaY);

        if (parentwindow.Position != newPosition)
        {
            windowDragHelper.SetWindowPosition(parentwindow, newPosition);
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

        if (parentwindow != null && IsWithinClickThreshold(parentwindow.Position, lastWindowPosition))
        {
            logger.LogDebug("手动拖动未改变窗口位置，触发点击事件");
            if (_pendingClickAction == DragClickAction.Button1)
            {
                IslandCallerService.ShowRandomStudent(1);
            }
            else if (_pendingClickAction == DragClickAction.Button2)
            {
                await new PersonalCall().ShowOwnedNoActivateAsync(parentwindow);
            }
        }

        _pendingClickAction = DragClickAction.None;
        _touchDragDelayElapsed = false;
        _touchDragThresholdElapsed = false;
    }

    private void ResetManualDragAnchor(Window window, Point pointerPosition)
    {
        _dragStartPointerPosition = pointerPosition;
        _dragStartPointerScreenPosition = window.PointToScreen(pointerPosition);
        _dragStartWindowPosition = window.Position;
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



