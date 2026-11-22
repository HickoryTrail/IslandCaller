using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using IslandCaller.Models;

namespace IslandCaller.Controls;

public partial class HoverLiquidControl : HoverControlBase
{
    private const double BaseContainerCornerRadius = 35;
    private const double BaseMiniButtonSize = 56;
    private const double BaseMiniButtonCornerRadius = 28;
    private const double BaseMiniButtonMargin = 12;
    private const double BaseMiniIconFontSize = 28;
    private const double PressScale = 0.88;
    private const double PressFeedbackScale = 0.92;
    private const double SpringOvershoot = 1.35;
    private bool _isMiniLayout;
    private readonly ScaleTransform _miniButtonPressScale = new();
    private DispatcherTimer? _pressAnimationTimer;
    private long _pressAnimationStart;
    private double _pressAnimationFrom = 1;
    private double _pressAnimationTo = 1;

    protected override Button PrimaryButton => _isMiniLayout ? MiniButton : Button1;
    protected override Button SecondaryButton => Button2;
    protected override TextBlock CallTextBlock => CallText;
    protected override InputElement DragSurface => DragSurfacePanel;

    public HoverLiquidControl()
    {
        InitializeComponent();
        MiniButton.RenderTransform = _miniButtonPressScale;
        MiniButton.AddHandler(InputElement.PointerPressedEvent, OnMiniPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        MiniButton.AddHandler(InputElement.PointerReleasedEvent, OnMiniPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        MiniButton.AddHandler(InputElement.PointerCaptureLostEvent, OnMiniPointerCaptureLost, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        InitializeHoverControl();
    }

    private void OnMiniPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _miniButtonPressScale.ScaleX = PressFeedbackScale;
        _miniButtonPressScale.ScaleY = PressFeedbackScale;
        AnimateMiniPress(PressScale);
    }

    private void OnMiniPointerReleased(object? sender, PointerReleasedEventArgs e) => AnimateMiniPress(1);

    private void OnMiniPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => AnimateMiniPress(1);

    private void AnimateMiniPress(double target)
    {
        _pressAnimationTimer?.Stop();
        _pressAnimationFrom = _miniButtonPressScale.ScaleX;
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
        _miniButtonPressScale.ScaleX = value;
        _miniButtonPressScale.ScaleY = value;
        if (progress >= 1)
        {
            _miniButtonPressScale.ScaleX = _pressAnimationTo;
            _miniButtonPressScale.ScaleY = _pressAnimationTo;
            _pressAnimationTimer?.Stop();
        }
    }

    protected override void ApplyThemeLayout(int hoverLayout)
    {
        bool isFullLayout = hoverLayout == 0;
        _isMiniLayout = hoverLayout == 2;
        var scalingFactor = GetScalingFactor();

        GlassContainer.IsVisible = !_isMiniLayout;
        GlassContainer.CornerRadius = BaseContainerCornerRadius * scalingFactor;
        MiniButton.IsVisible = _isMiniLayout;
        MiniButton.Width = BaseMiniButtonSize * scalingFactor;
        MiniButton.Height = BaseMiniButtonSize * scalingFactor;
        MiniButton.CornerRadius = BaseMiniButtonCornerRadius * scalingFactor;
        MiniButton.Margin = new Thickness(BaseMiniButtonMargin * scalingFactor);
        MiniIcon.FontSize = BaseMiniIconFontSize * scalingFactor;
        CallTextBlock.IsVisible = isFullLayout;
        Button1.Width = isFullLayout ? 88 : 56;
        Button1.Height = 56;
        Button1.CornerRadius = new CornerRadius(28);
        Button2.IsVisible = !_isMiniLayout;
        Button2.Width = 56;
        Button2.Height = 56;
        Button2.CornerRadius = new CornerRadius(28);
    }

    private static double GetScalingFactor()
    {
        var scalingFactor = Settings.Instance.Hover.ScalingFactor;
        return double.IsFinite(scalingFactor) && scalingFactor > 0
            ? scalingFactor
            : 1;
    }
}
