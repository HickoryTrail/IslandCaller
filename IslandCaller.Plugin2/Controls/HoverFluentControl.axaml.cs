using Avalonia.Animation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace IslandCaller.Controls;

public partial class HoverFluentControl : HoverControlBase
{
    protected override Button PrimaryButton => Button1;
    protected override Button SecondaryButton => Button2;
    protected override TextBlock CallTextBlock => CallText;
    protected override InputElement DragSurface => DragSurfacePanel;

    public HoverFluentControl()
    {
        InitializeComponent();
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

    protected override void ApplyThemeLayout(int hoverLayout, int layoutDirection)
    {
        bool isFullLayout = hoverLayout == 0;
        bool isSimpleLayout = hoverLayout == 3;
        bool isVertical = layoutDirection == 1;
        double callWidth = ConfiguredCallButtonWidth();

        // 竖版：容器改为垂直排列。
        DragSurfacePanel.Orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal;

        CallTextBlock.IsVisible = isFullLayout || isSimpleLayout;
        PrimaryButton.Width = (isFullLayout || isSimpleLayout) ? callWidth : 56;
        PrimaryButton.Height = 56;

        // 界面圆角：统一应用到悬浮窗按钮，收敛到按钮半高（56/2=28）以内。
        var radius = new CornerRadius(ClampCornerRadius(28));
        PrimaryButton.CornerRadius = radius;
        SecondaryButton.IsVisible = isFullLayout || hoverLayout == 1;
        // 竖版完整态下与主按钮同宽以左右对齐；宽度为「自动」时退回 56，避免次按钮塌缩。
        SecondaryButton.Width = (isVertical && isFullLayout && !double.IsNaN(callWidth)) ? callWidth : 56;
        SecondaryButton.CornerRadius = radius;
    }
}
