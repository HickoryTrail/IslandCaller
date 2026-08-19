using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Services.IslandCallerService;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Controls;

/// <summary>
/// Shares hover button actions and pointer-driven drag behavior across themes.
/// </summary>
public abstract class HoverControlBase : UserControl
{
    private const int TouchDragIgnoreDurationMs = 75;
    private const int ClickMoveThresholdPx = 10;
    private const int TouchMoveDeadZonePx = 2;
    private readonly IslandCallerService _islandCallerService;
    private readonly ILogger<HoverControlBase> _logger;
    private readonly WindowDragHelper _windowDragHelper;
    private Window? _parentWindow;
    private PixelPoint _lastWindowPosition;
    private long _lastDragTime;
    private bool _isManualDragging;
    private IPointer? _dragPointer;
    private PixelPoint _dragStartWindowPosition;
    private Point _dragStartPointerPosition;
    private PixelPoint _dragStartPointerScreenPosition;
    private PixelPoint _lastAcceptedPointerScreenPosition;
    private DragClickAction _pendingClickAction;
    private long _manualDragStartTime;
    private bool _touchDragDelayElapsed;
    private bool _touchDragThresholdElapsed;
    private Color? _secondaryButtonForeground;
    private Color? _primaryButtonForeground;

    protected abstract Button PrimaryButton { get; }
    protected abstract Button SecondaryButton { get; }
    protected abstract TextBlock CallTextBlock { get; }
    protected abstract InputElement DragSurface { get; }

    protected HoverControlBase()
    {
        _islandCallerService = IAppHost.GetService<IslandCallerService>();
        _logger = IAppHost.GetService<ILogger<HoverControlBase>>();
        _windowDragHelper = IAppHost.GetService<WindowDragHelper>();
    }

    protected void InitializeHoverControl()
    {
        SecondaryButton.PropertyChanged += SecondaryButtonOnPropertyChanged;
        Settings.Instance.Hover.PropertyChanged += HoverSettingOnPropertyChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        ApplyHoverLayout();
        DragSurface.AddHandler(InputElement.PointerPressedEvent, DragSurfaceOnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        DragSurface.AddHandler(InputElement.PointerMovedEvent, DragPointerMoved, RoutingStrategies.Tunnel, true);
        DragSurface.AddHandler(InputElement.PointerReleasedEvent, DragPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        DragSurface.AddHandler(InputElement.PointerCaptureLostEvent, DragPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    protected abstract void ApplyThemeLayout(int hoverLayout);

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Settings.Instance.Hover.PropertyChanged -= HoverSettingOnPropertyChanged;
        SecondaryButton.PropertyChanged -= SecondaryButtonOnPropertyChanged;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    private void HoverSettingOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HoverSetting.HoverLayout)
            or nameof(HoverSetting.ScalingFactor))
        {
            Dispatcher.UIThread.Post(ApplyHoverLayout, DispatcherPriority.Render);
        }
    }

    private void ApplyHoverLayout()
    {
        ApplyThemeLayout(Settings.Instance.Hover.HoverLayout);
        if (TopLevel.GetTopLevel(this) is IHoverWindow hoverWindow)
        {
            hoverWindow.RequestContentSizeUpdate();
        }
    }

    private void SecondaryButtonOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsEffectivelyEnabledProperty && !SecondaryButton.IsEffectivelyEnabled)
        {
            ResetSecondaryButtonForeground();
        }
    }

    public bool IsSecondaryButtonEffectivelyEnabled => SecondaryButton.IsVisible && SecondaryButton.IsEffectivelyEnabled;

    public bool TryGetSecondaryButtonScreenRect(out PixelRect rect)
    {
        return TryGetButtonScreenRect(SecondaryButton, out rect);
    }

    public bool TryGetPrimaryButtonScreenRect(out PixelRect rect)
    {
        return TryGetButtonScreenRect(PrimaryButton, out rect);
    }

    private static bool TryGetButtonScreenRect(Button button, out PixelRect rect)
    {
        rect = default;
        if (!button.IsVisible || button.Bounds.Width <= 0 || button.Bounds.Height <= 0)
        {
            return false;
        }

        var topLeft = button.PointToScreen(default);
        var bottomRight = button.PointToScreen(new Point(button.Bounds.Width, button.Bounds.Height));
        int x = Math.Min(topLeft.X, bottomRight.X);
        int y = Math.Min(topLeft.Y, bottomRight.Y);
        int width = Math.Abs(bottomRight.X - topLeft.X);
        int height = Math.Abs(bottomRight.Y - topLeft.Y);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        rect = new PixelRect(x, y, width, height);
        return true;
    }

    public void SetSecondaryButtonForeground(Color foreground)
    {
        if (!SecondaryButton.IsEffectivelyEnabled)
        {
            ResetSecondaryButtonForeground();
            return;
        }

        if (_secondaryButtonForeground == foreground)
        {
            return;
        }

        _secondaryButtonForeground = foreground;
        SecondaryButton.Foreground = foreground == Colors.White ? Brushes.White : Brushes.Black;
    }

    public void SetPrimaryButtonForeground(Color foreground)
    {
        if (!PrimaryButton.IsEffectivelyEnabled || _primaryButtonForeground == foreground)
        {
            return;
        }

        _primaryButtonForeground = foreground;
        PrimaryButton.Foreground = foreground == Colors.White ? Brushes.White : Brushes.Black;
    }

    public void ResetSecondaryButtonForeground()
    {
        _secondaryButtonForeground = null;
        var transitions = SecondaryButton.Transitions;
        SecondaryButton.Transitions = null;
        SecondaryButton.ClearValue(TemplatedControl.ForegroundProperty);
        SecondaryButton.Transitions = transitions;
    }

    private async void DragSurfaceOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (Environment.TickCount64 - _lastDragTime < 50)
        {
            _lastDragTime = Environment.TickCount64;
            return;
        }

        var clickAction = GetClickAction(e);
        _lastDragTime = Environment.TickCount64;
        _parentWindow = TopLevel.GetTopLevel(this) as Window ?? this.FindAncestorOfType<Window>();
        if (_parentWindow is null)
        {
            _logger.LogWarning("无法获取悬浮窗宿主，跳过拖动。");
            return;
        }

        _lastWindowPosition = _parentWindow.Position;
        if (TryStartManualDrag(_parentWindow, e, clickAction))
        {
            return;
        }

        if (_parentWindow is IHoverWindow hoverWindow)
        {
            hoverWindow.BeginDrag();
        }

        await _windowDragHelper.DragMoveAsync(_parentWindow, e.Pointer.Type);
        if (_parentWindow is IHoverWindow completedHoverWindow)
        {
            completedHoverWindow.EndDragAndClamp();
        }

        if (clickAction != DragClickAction.None && IsWithinClickThreshold(_parentWindow.Position, _lastWindowPosition))
        {
            await InvokeClickActionAsync(clickAction, _parentWindow);
        }
    }

    private DragClickAction GetClickAction(PointerPressedEventArgs e)
    {
        if (PrimaryButton.IsEnabled && IsPointerWithin(PrimaryButton, e))
        {
            return DragClickAction.Primary;
        }

        return SecondaryButton.IsVisible && SecondaryButton.IsEnabled && IsPointerWithin(SecondaryButton, e)
            ? DragClickAction.Secondary
            : DragClickAction.None;
    }

    private static bool IsPointerWithin(Control control, PointerEventArgs e)
    {
        var position = e.GetPosition(control);
        return position.X >= 0 && position.Y >= 0 && position.X <= control.Bounds.Width && position.Y <= control.Bounds.Height;
    }

    private async Task InvokeClickActionAsync(DragClickAction clickAction, Window owner)
    {
        if (clickAction == DragClickAction.Primary)
        {
            _islandCallerService.ShowRandomStudent(1);
            return;
        }

        if (clickAction == DragClickAction.Secondary)
        {
            await new PersonalCall().ShowOwnedNoActivateAsync(owner);
        }
    }

    private bool TryStartManualDrag(Window window, PointerPressedEventArgs e, DragClickAction clickAction)
    {
        if (e.Pointer.Type is not (PointerType.Touch or PointerType.Pen))
        {
            return false;
        }

        _isManualDragging = true;
        _dragPointer = e.Pointer;
        _manualDragStartTime = Environment.TickCount64;
        _touchDragDelayElapsed = false;
        _touchDragThresholdElapsed = false;
        _pendingClickAction = clickAction;
        ResetManualDragAnchor(window, e.GetPosition(window));
        if (window is IHoverWindow hoverWindow)
        {
            hoverWindow.BeginDrag();
        }

        e.Pointer.Capture(DragSurface);
        return true;
    }

    private void DragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isManualDragging || _dragPointer is null || e.Pointer != _dragPointer || _parentWindow is null)
        {
            return;
        }

        var current = e.GetPosition(_parentWindow);
        if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
        {
            long elapsed = Environment.TickCount64 - _manualDragStartTime;
            if (!_touchDragDelayElapsed)
            {
                if (elapsed < TouchDragIgnoreDurationMs)
                {
                    return;
                }

                _touchDragDelayElapsed = true;
                ResetManualDragAnchor(_parentWindow, current);
                return;
            }

            if (!_touchDragThresholdElapsed)
            {
                if (Math.Abs(current.X - _dragStartPointerPosition.X) < ClickMoveThresholdPx &&
                    Math.Abs(current.Y - _dragStartPointerPosition.Y) < ClickMoveThresholdPx)
                {
                    return;
                }

                _touchDragThresholdElapsed = true;
                ResetManualDragAnchor(_parentWindow, current);
                return;
            }
        }

        var currentScreen = _parentWindow.PointToScreen(current);
        if (IsWithinTouchMoveDeadZone(currentScreen, _lastAcceptedPointerScreenPosition))
        {
            return;
        }

        var newPosition = new PixelPoint(
            _dragStartWindowPosition.X + currentScreen.X - _dragStartPointerScreenPosition.X,
            _dragStartWindowPosition.Y + currentScreen.Y - _dragStartPointerScreenPosition.Y);
        if (_parentWindow.Position != newPosition && _windowDragHelper.SetWindowPosition(_parentWindow, newPosition))
        {
            _lastAcceptedPointerScreenPosition = currentScreen;
        }
    }

    private async void DragPointerReleased(object? sender, PointerReleasedEventArgs e) => await EndManualDragAsync(e.Pointer);

    private async void DragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => await EndManualDragAsync(e.Pointer);

    private async Task EndManualDragAsync(IPointer pointer)
    {
        if (!_isManualDragging || _dragPointer is null || pointer != _dragPointer)
        {
            return;
        }

        _isManualDragging = false;
        _dragPointer = null;
        pointer.Capture(null);
        if (_parentWindow is IHoverWindow hoverWindow)
        {
            hoverWindow.EndDragAndClamp();
        }

        if (_parentWindow is not null && _pendingClickAction != DragClickAction.None &&
            IsWithinClickThreshold(_parentWindow.Position, _lastWindowPosition))
        {
            await InvokeClickActionAsync(_pendingClickAction, _parentWindow);
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
        int deltaX = currentPosition.X - acceptedPosition.X;
        int deltaY = currentPosition.Y - acceptedPosition.Y;
        return deltaX * deltaX + deltaY * deltaY <= TouchMoveDeadZonePx * TouchMoveDeadZonePx;
    }

    private static bool IsWithinClickThreshold(PixelPoint currentPosition, PixelPoint originalPosition) =>
        currentPosition == originalPosition ||
        (Math.Abs(currentPosition.X - originalPosition.X) < ClickMoveThresholdPx &&
         Math.Abs(currentPosition.Y - originalPosition.Y) < ClickMoveThresholdPx);

    private enum DragClickAction
    {
        None,
        Primary,
        Secondary
    }
}
