using Avalonia;
using Avalonia.Controls;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using MorerialsAvalonia;
using MorerialsAvalonia.Materials.LiquidGlass;
using System.ComponentModel;

namespace IslandCaller.Views;

public partial class LiquidShower : Window
{
    private const double MinimumGlassHeight = 110;

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

    /// <summary>
    /// 根据内容实际高度调整玻璃容器尺寸，避免结果字号较大时被固定高度裁剪。
    /// 圆角取「外观设置中的界面圆角」并按高度收拢，最小保持 110 高度。
    /// </summary>
    public void ApplyGlassExtent(double height, double cornerRadius)
    {
        var safeHeight = Math.Max(MinimumGlassHeight, height);
        GlassContainer.Height = safeHeight;
        GlassContainer.CornerRadius = Math.Min(Math.Max(0, cornerRadius), safeHeight / 2);
    }

    protected override void OnClosed(EventArgs e)
    {
        // 反注册诊断订阅，避免关闭后 MaterialHost 后台渲染线程通过诊断回调
        // 访问已销毁窗口（use-after-free），并解除窗口与 MaterialHost 的相互引用。
        Materials.Diagnostics.PropertyChanged -= OnMaterialDiagnosticsChanged;
        DisplayContent.Content = null;

        // 在窗口关闭流程结束前主动分离 MaterialHost，确保 StopHostSession
        // 释放 D3D11 渲染器、Desktop Duplication 捕获与合成表面等 GPU 资源，
        // 避免频繁点名时内存持续增长。
        if (ReferenceEquals(Content, Materials))
        {
            _logger.LogInformation("LiquidShower 关闭：正在分离 MaterialHost...");
            Content = null;
        }

        base.OnClosed(e);
    }

    private void OnMaterialDiagnosticsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MaterialRenderDiagnostics.Error) &&
            !string.IsNullOrWhiteSpace(Materials.Diagnostics.Error))
        {
            _logger.LogError(
                "LiquidGlass 展示窗口错误：{Error} | IsOperational={IsOperational} | CaptureState={CaptureState}",
                Materials.Diagnostics.Error,
                Materials.Diagnostics.IsOperational,
                Materials.Diagnostics.CaptureState);
        }
    }
}
