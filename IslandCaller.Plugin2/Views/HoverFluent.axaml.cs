using Avalonia.Media;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Views;

public partial class HoverFluent : HoverWindowBase
{
    private readonly ILogger<HoverFluent> _logger = IAppHost.GetService<ILogger<HoverFluent>>();
    private readonly ScreenBrightnessHelper _screenBrightnessHelper = IAppHost.GetService<ScreenBrightnessHelper>();

    public HoverFluent()
    {
        InitializeComponent();
        InitializeHoverWindow(HoverControl, ScaledContent);
    }

    protected override void ApplyThemeTopmost()
    {
        if (!HoverControl.IsSecondaryButtonEffectivelyEnabled)
        {
            HoverControl.ResetSecondaryButtonForeground();
            return;
        }

        var foreground = Colors.Black;
        if (HoverControl.TryGetSecondaryButtonScreenRect(out var buttonRect)
            && _screenBrightnessHelper.TryGetAverageRelativeLuminance(buttonRect, out var luminance))
        {
            foreground = ScreenBrightnessHelper.GetRecommendedForeground(luminance);
        }

        HoverControl.SetSecondaryButtonForeground(foreground);
        _logger.LogTrace("已更新 Fluent 次按钮前景色。");
    }
}
