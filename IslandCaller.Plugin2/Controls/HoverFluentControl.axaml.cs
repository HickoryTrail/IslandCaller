using Avalonia.Animation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

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

    protected override void ApplyThemeLayout(int hoverLayout)
    {
        bool isFullLayout = hoverLayout == 0;
        bool isMiniLayout = hoverLayout == 2;

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
