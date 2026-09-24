namespace IslandCaller.Helpers
{
    internal static class CheckDependences
    {
        /// <summary>
        /// 检查 OmniTTS 是否可用。引用 OmniTTS.Shared 的加载动作已隔离在
        /// <see cref="OmniTtsBridge"/> 内部，依赖被系统策略拦截时返回 false 而非抛异常。
        /// </summary>
        internal static bool CheckOmniTTS() => OmniTtsBridge.IsAvailable();
    }
}
