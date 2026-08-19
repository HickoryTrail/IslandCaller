using Avalonia.Controls;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using MorerialsAvalonia;
using MorerialsAvalonia.Materials.LiquidGlass;
using System.ComponentModel;

namespace IslandCaller.Views;

public partial class LiquidShower : Window
{
    private readonly ILogger<LiquidShower> _logger = IAppHost.GetService<ILogger<LiquidShower>>();
    public LiquidGlassMaterial GlassMaterial { get; } =
      LiquidGlassProfiles.Reference with
      {
          BlurRadius = 16,
          BlurDownsampleScale = 0.25
      };

    public LiquidShower()
    {
        InitializeComponent();
        GlassContainer.Material = GlassMaterial;
        Materials.Diagnostics.PropertyChanged += OnMaterialDiagnosticsChanged;
    }

    public void SetDisplayContent(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);
        DisplayContent.Content = content;
    }

    private void OnMaterialDiagnosticsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MaterialRenderDiagnostics.Error) &&
            !string.IsNullOrWhiteSpace(Materials.Diagnostics.Error))
        {
            _logger.LogError("LiquidGlass 展示窗口初始化失败：{Error}", Materials.Diagnostics.Error);
        }
    }
}
