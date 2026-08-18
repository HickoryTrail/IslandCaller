using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using MorerialsAvalonia;
using System.ComponentModel;

namespace IslandCaller.Views;

public partial class HoverLiquid : HoverWindowBase
{
    private readonly ILogger<HoverLiquid> _logger = IAppHost.GetService<ILogger<HoverLiquid>>();

    public HoverLiquid()
    {
        InitializeComponent();
        Materials.Diagnostics.PropertyChanged += OnMaterialDiagnosticsChanged;
        InitializeHoverWindow(HoverControl, HoverControl);
    }

    protected override void UpdateWindowChrome(int hoverLayout)
    {
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [Avalonia.Controls.WindowTransparencyLevel.Transparent];
    }

    private void OnMaterialDiagnosticsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MaterialRenderDiagnostics.Error) &&
            !string.IsNullOrWhiteSpace(Materials.Diagnostics.Error))
        {
            _logger.LogError("LiquidGlass 悬浮窗初始化失败：{Error}", Materials.Diagnostics.Error);
        }
    }
}
