using Avalonia.Animation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace IslandCaller.Controls;

public partial class HoverFluentControl : HoverControlBase
{
    private const double PressScale = 0.88;
    private const double PressFeedbackScale = 0.92;
    private const double SpringOvershoot = 1.35;
    private readonly ScaleTransform _miniPressScale = new();
    private DispatcherTimer? _pressAnimationTimer;
    private long _pressAnimationStart;
    private double _pressAnimationFrom = 1;
    private double _pressAnimationTo = 1;
    private bool _isMiniLayout;

    protected override Button PrimaryButton => Button1;
    protected override Button SecondaryButton => Button2;
    protected override TextBlock CallTextBlock => CallText;
    protected override InputElement DragSurface => DragSurfacePanel;

    public HoverFluentControl()
    {
        InitializeComponent();
        PrimaryButton.RenderTransform = _miniPressScale;
        PrimaryButton.AddHandler(InputElement.PointerPressedEvent, OnMiniPointerPressed, RoutingStrategies.Tunnel, true);
        PrimaryButton.AddHandler(InputElement.PointerReleasedEvent, OnMiniPointerReleased, RoutingStrategies.Tunnel, true);
        PrimaryButton.AddHandler(InputElement.PointerCaptureLostEvent, OnMiniPointerCaptureLost, RoutingStrategies.Tunnel, true);
        SecondaryButton.Transitions = new Transitions
        {
            new BrushTransition
            {
                Property = TemplatedControl.ForegroundProperty,
                Duration = TimeSpan.FromMilliseconds(250)
            }
        };
        InitializeHoverControl();
    }

    private void OnMiniPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isMiniLayout) return;
        _miniPressScale.ScaleX = PressFeedbackScale;
        _miniPressScale.ScaleY = PressFeedbackScale;
        AnimateMiniPress(PressScale);
    }

    private void OnMiniPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isMiniLayout) AnimateMiniPress(1);
    }

    private void OnMiniPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isMiniLayout) AnimateMiniPress(1);
    }

    private void AnimateMiniPress(double target)
    {
        _pressAnimationTimer?.Stop();
        _pressAnimationFrom = _miniPressScale.ScaleX;
        _pressAnimationTo = target;
        _pressAnimationStart = Environment.TickCount64;
        _pressAnimationTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _pressAnimationTimer.Tick -= PressAnimationTick;
        _pressAnimationTimer.Tick += PressAnimationTick;
        _pressAnimationTimer.Start();
    }

    private void PressAnimationTick(object? sender, EventArgs e)
    {
        double progress = Math.Clamp((Environment.TickCount64 - _pressAnimationStart) / 300d, 0, 1);
        double easedProgress = 1 + (SpringOvershoot + 1) * Math.Pow(progress - 1, 3)
            + SpringOvershoot * Math.Pow(progress - 1, 2);
        double value = _pressAnimationFrom + (_pressAnimationTo - _pressAnimationFrom) * easedProgress;
        _miniPressScale.ScaleX = value;
        _miniPressScale.ScaleY = value;
        if (progress >= 1)
        {
            _miniPressScale.ScaleX = _pressAnimationTo;
            _miniPressScale.ScaleY = _pressAnimationTo;
            _pressAnimationTimer?.Stop();
        }
    }

    protected override void ApplyThemeLayout(int hoverLayout)
    {
        bool isFullLayout = hoverLayout == 0;
        bool isMiniLayout = hoverLayout == 2;
        _isMiniLayout = isMiniLayout;

        CallTextBlock.IsVisible = isFullLayout;
        PrimaryButton.Width = isFullLayout ? 88 : 56;
        if (isMiniLayout)
        {
            PrimaryButton.CornerRadius = new CornerRadius(28);
        }
        else
        {
            PrimaryButton.ClearValue(TemplatedControl.CornerRadiusProperty);
        }

        SecondaryButton.IsVisible = !isMiniLayout;
        SecondaryButton.Width = 56;
    }
}
