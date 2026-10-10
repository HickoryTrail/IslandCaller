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

    protected override void OnClosed(EventArgs e)
    {
        // 反注册诊断订阅，避免关闭后 MaterialHost 的后台渲染线程通过诊断回调
        // 访问已销毁窗口（use-after-free），并解除窗口与 MaterialHost 的相互引用。
        Materials.Diagnostics.PropertyChanged -= OnMaterialDiagnosticsChanged;
        base.OnClosed(e);

        // 主动把 MaterialHost 从视觉树分离，确保 StopHostSession 被调用，
        // 及时释放 D3D11 渲染器、Desktop Duplication 捕获与合成表面等 GPU 资源。
        if (ReferenceEquals(Content, Materials))
        {
            Content = null;
        }
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
