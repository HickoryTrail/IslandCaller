using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;
using MorerialsAvalonia;

namespace IslandCaller.Services;

/// <summary>
/// Prepares the GPU material pipeline once and prevents unavailable themes from being selected.
/// </summary>
internal sealed class LiquidGlassRuntime(ILogger<LiquidGlassRuntime> logger)
{
    private static readonly Uri MaterialThemeUri = new("avares://MorerialsAvalonia/Themes/Generic.axaml");
    private static bool _materialThemeRegistered;
    private readonly ILogger<LiquidGlassRuntime> _logger = logger;

    public bool IsReady { get; private set; }

    public async Task PrewarmAsync()
    {
        try
        {
            RegisterMaterialTheme();
            var result = await MaterialShaderCompiler.EnsureCompiledAsync();
            IsReady = true;
            _logger.LogInformation(
                "LiquidGlass 着色器已就绪：编译 {Compiled}，复用 {Reused}，缓存 {CacheDirectory}",
                result.CompiledShaderCount,
                result.ReusedShaderCount,
                result.CacheDirectory);
        }
        catch (Exception exception)
        {
            IsReady = false;
            _logger.LogError(exception, "LiquidGlass 着色器预热失败，已回退至 Fluent 主题。");
            RevertUnavailableThemes();
        }
    }

    public bool CanUseHoverTheme()
    {
        if (Settings.Instance.Hover.HoverTheme != 1 || IsReady)
        {
            return Settings.Instance.Hover.HoverTheme == 1;
        }

        RevertUnavailableThemes();
        return false;
    }

    public bool CanUseShowerTheme()
    {
        if (Settings.Instance.Call.ShowerTheme != 1 || IsReady)
        {
            return Settings.Instance.Call.ShowerTheme == 1;
        }

        RevertUnavailableThemes();
        return false;
    }

    private static void RevertUnavailableThemes()
    {
        if (Settings.Instance.Hover.HoverTheme == 1)
        {
            Settings.Instance.Hover.HoverTheme = 0;
        }

        if (Settings.Instance.Call.ShowerTheme == 1)
        {
            Settings.Instance.Call.ShowerTheme = 0;
        }
    }

    private void RegisterMaterialTheme()
    {
        if (_materialThemeRegistered)
        {
            return;
        }

        var application = Application.Current
            ?? throw new InvalidOperationException("Avalonia 应用尚未初始化，无法加载 LiquidGlass 样式。");
        application.Styles.Add(new StyleInclude(MaterialThemeUri)
        {
            Source = MaterialThemeUri
        });
        _materialThemeRegistered = true;
        _logger.LogInformation("已在应用级加载 MorerialsAvalonia 默认模板。");
    }
}
