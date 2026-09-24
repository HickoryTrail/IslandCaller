using System.Runtime.CompilerServices;
using ClassIsland.Shared;
using OmniTTS.Shared;

namespace IslandCaller.Helpers;

/// <summary>
/// OmniTTS 可选依赖的安全桥接。
///
/// <para>OmniTTS.Shared.dll 是随插件分发的可选依赖，可能因未安装、文件缺失，
/// 或被系统安全策略拦截（例如 Windows 11「智能应用控制」返回 Win32 4551
/// 「应用程序控制策略已阻止此文件」）而无法加载。</para>
///
/// <para>CLR 是在**执行到引用该程序集的方法时**才去加载它的，所以这里把对
/// OmniTTS.Shared 的所有类型引用集中到几个独立的、标记了
/// <see cref="MethodImplOptions.NoInlining"/> 的方法里，并由调用方捕获加载异常。
/// 这样即使该依赖完全不可用，插件主体仍能正常启动与点名，只是没有 OmniTTS 语音。</para>
/// </summary>
internal static class OmniTtsBridge
{
    /// <summary>判断加载失败类异常（据此决定是否可以安全降级，而不吞掉其它真实错误）。</summary>
    private static bool IsLoadFailure(Exception ex) =>
        ex is FileNotFoundException
            or FileLoadException
            or TypeLoadException
            or BadImageFormatException
            or MissingMethodException
            or MissingFieldException;

    /// <summary>OmniTTS 服务是否可用（未安装或加载被拦截时返回 false）。</summary>
    public static bool IsAvailable()
    {
        try
        {
            return ResolveCore() is not null;
        }
        catch (Exception ex) when (IsLoadFailure(ex))
        {
            return false;
        }
    }

    /// <summary>尝试获取 OmniTTS 服务实例，加载失败时返回 null。</summary>
    public static object? TryResolve()
    {
        try
        {
            return ResolveCore();
        }
        catch (Exception ex) when (IsLoadFailure(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// 调用 OmniTTS 播放语音。服务为 null 或依赖加载失败时返回 false（调用方据此记录日志）。
    /// </summary>
    public static bool TryPlay(object? service, string text, CancellationToken token)
    {
        if (service is null)
        {
            return false;
        }

        try
        {
            return PlayCore(service, text, token);
        }
        catch (Exception ex) when (IsLoadFailure(ex))
        {
            return false;
        }
    }

    // —— 以下两个方法之外的任何地方都不得直接引用 OmniTTS.Shared 的类型 ——

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object? ResolveCore() => IAppHost.TryGetService<IOmniTTS>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool PlayCore(object service, string text, CancellationToken token)
    {
        if (service is IOmniTTS omniTts)
        {
            omniTts.PlayAudio(text, token);
            return true;
        }

        return false;
    }
}
