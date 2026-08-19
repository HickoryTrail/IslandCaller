using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using IslandCaller.Models;

namespace IslandCaller.Controls;

public partial class HoverLiquidControl : HoverControlBase
{
    private const double BaseContainerCornerRadius = 35;
    private const double BaseMiniButtonSize = 56;
    private const double BaseMiniButtonCornerRadius = 28;
    private const double BaseMiniButtonMargin = 12;
    private const double BaseMiniIconFontSize = 28;
    private bool _isMiniLayout;

    protected override Button PrimaryButton => _isMiniLayout ? MiniButton : Button1;
    protected override Button SecondaryButton => Button2;
    protected override TextBlock CallTextBlock => CallText;
    protected override InputElement DragSurface => DragSurfacePanel;

    public HoverLiquidControl()
    {
        InitializeComponent();
        InitializeHoverControl();
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
