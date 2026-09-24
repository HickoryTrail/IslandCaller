using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using IslandCaller.Models;

namespace IslandCaller.Controls;

public partial class HoverLiquidControl : HoverControlBase
{
    private const double BaseContainerCornerRadius = 35;
    private const double BaseMiniButtonSize = 56;
    private const double BaseMiniButtonCornerRadius = 28;
    private const double BaseMiniButtonMargin = 12;
    private const double BaseMiniIconSize = 24;
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

    protected override void ApplyThemeLayout(int hoverLayout, int layoutDirection)
    {
        bool isFullLayout = hoverLayout == 0;
        bool isSimpleLayout = hoverLayout == 3;
        bool isVertical = layoutDirection == 1;
        _isMiniLayout = hoverLayout == 2;
        var scalingFactor = GetScalingFactor();
        double callWidth = ConfiguredCallButtonWidth();

        // 竖版：按钮容器改为垂直排列；容器间距/内边距沿用既有值。
        ButtonsPanel.Orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal;

        GlassContainer.IsVisible = !_isMiniLayout;
        // 界面圆角：玻璃容器半径跟随配置，收敛到容器半高（35）后随缩放因子放大。
        GlassContainer.CornerRadius = ClampCornerRadius(BaseContainerCornerRadius) * scalingFactor;
        MiniButton.IsVisible = _isMiniLayout;
        MiniButton.Width = BaseMiniButtonSize * scalingFactor;
        MiniButton.Height = BaseMiniButtonSize * scalingFactor;
        MiniButton.CornerRadius = ClampCornerRadius(BaseMiniButtonCornerRadius) * scalingFactor;
        MiniButton.Margin = new Thickness(BaseMiniButtonMargin * scalingFactor);
        MiniIcon.Width = BaseMiniIconSize * scalingFactor;
        MiniIcon.Height = BaseMiniIconSize * scalingFactor;
        CallTextBlock.IsVisible = isFullLayout || isSimpleLayout;
        Button1.Width = (isFullLayout || isSimpleLayout) ? callWidth : 56;
        Button1.Height = 56;
        Button1.CornerRadius = new CornerRadius(ClampCornerRadius(28));
        Button2.IsVisible = isFullLayout || hoverLayout == 1;
        // 竖版完整态下与主按钮同宽以左右对齐；宽度为「自动」时退回 56，避免次按钮塌缩。
        Button2.Width = (isVertical && isFullLayout && !double.IsNaN(callWidth)) ? callWidth : 56;
        Button2.Height = 56;
        Button2.CornerRadius = new CornerRadius(ClampCornerRadius(28));
    }

    private static double GetScalingFactor()
    {
        var scalingFactor = Settings.Instance.Hover.ScalingFactor;
        return double.IsFinite(scalingFactor) && scalingFactor > 0
            ? scalingFactor
            : 1;
    }
}
