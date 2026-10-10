using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

/// <summary>
/// 置顶增强引擎。
///
/// 目标：把 IslandCaller 的悬浮窗 / 点名结果窗口的置顶优先级提升到用户态 Win32
/// 所能达到的最高级别 —— 置顶带（topmost band）的最顶端，并在任何其它窗口
/// 抢占前台后立即夺回。
///
/// 多机制同时生效（互相独立、可单独开关）：
///   1. 【高频 Z 序重推】以极短周期（默认 250ms，IslandCaller 自带循环为 3000ms）
///      反复调用 SetWindowPos(HWND_TOPMOST)。置顶窗口始终位于普通窗口之上；
///      在置顶带内部，最近一次被置顶的窗口位于最上方，因此高频重推可以持续压制
///      其它"一次性置顶"的窗口（如播放器悬浮窗、任务管理器等）。
///   2. 【前台事件钩子】通过 SetWinEventHook 监听全局 EVENT_SYSTEM_FOREGROUND，
///      一旦任何窗口成为前台（用户切应用、全屏 PPT / 直播 / 白板抢焦点等），
///      立即把所有目标窗口重新推到置顶带最上方，响应近乎实时。
///   3. 【扩展样式强化】SetWindowLongPtr 为窗口追加 WS_EX_TOPMOST / WS_EX_TOOLWINDOW
///      / WS_EX_NOACTIVATE：确保置顶带归属、从 Alt+Tab 隐藏、且永不抢焦点。
///   4. 【Z 序校验与自动恢复】每次扫描后通过 GetWindow(GW_HWNDPREV) 沿 Z 序链向上
///      检查，若仍有其它可见窗口压在本窗口之上（置顶优先级被抢占），立即再推一次，
///      实现"失效即恢复"。
///   5. 【全屏抢占检测】当前台窗口矩形完全覆盖显示器（PPT 放映 / 播放器全屏 /
///      白板全屏等）时，立即对全部目标窗口整体重推，对抗全屏场景下的遮挡。
///   6. 【窗口发现】周期性扫描 Avalonia 桌面生命周期窗口集合，通过程序集名 /
///      类型名 / 标题关键词识别 IslandCaller 的窗口（跨插件程序集隔离，
///      无需编译期依赖）。
///   7. 【UIA 增强检测】基于 UI Automation 语义（DWM cloaked / offscreen 属性，
///      即 UIA IsOffscreen 的底层数据源）识别被系统隐藏的 UWP / 现代化窗口：
///      全屏检测跳过 cloaked 前台窗口，Z 序校验只认"真正可见"的遮挡者，
///      并放宽 2px 容差以覆盖 DPI 缩放下 UWP 全屏窗口的亚像素差异。
///
/// 说明：用户态普通窗口无法覆盖"独占全屏"（exclusive fullscreen，多见于游戏），
/// 但课堂场景常用的全屏演示（PPT / 白板 / 直播 / 视频）均为无边框窗口，
/// 置顶带可以稳定压在其上方，达到"最高级置顶"。
/// </summary>
public sealed class TopmostEnhancerService : IDisposable
{
    /// <summary>已知的 IslandCaller 窗口类型名（与程序集匹配互为兜底）。</summary>
    private static readonly string[] KnownWindowTypeNames =
    [
        "HoverFluent", "HoverLiquid",        // 悬浮窗（Fluent / LiquidGlass 主题）
        "FluentShower", "LiquidShower",      // 点名结果展示窗口
        "PersonalCall"                       // 自定义抽取窗口
    ];

    private const long HookThrottleMs = 50;  // 前台钩子节流，避免事件风暴

    private readonly ILogger<TopmostEnhancerService> _logger;
    private readonly TopmostSetting _settings;

    private readonly HashSet<IntPtr> _trackedHwnds = new();
    private DispatcherTimer? _timer;
    private IntPtr _winEventHook;
    private NativeMethods.WinEventDelegate? _winEventDelegate; // 防止 GC 回收委托
    private long _lastHookApplyTick;
    private bool _disposed;

    public TopmostEnhancerService(ILogger<TopmostEnhancerService> logger, TopmostSetting settings)
    {
        _logger = logger;
        _settings = settings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    /// <summary>启动增强引擎（在 AppStarted 后调用，此时 Avalonia Application 已就绪）。</summary>
    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TopmostEnhancerService));
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("置顶增强仅支持 Windows，已跳过启动。");
            return;
        }

        RestartTimer();
        if (_settings.EnableForegroundHook)
        {
            InstallForegroundHook();
        }

        // 立即执行一次扫描，让已打开的 IslandCaller 窗口马上获得最高置顶。
        ScanAndApply();
        _logger.LogInformation("IslandCaller 置顶增强引擎已启动（周期 {Interval}ms，前台钩子 {Hook}）。",
            _settings.IntervalMs, _settings.EnableForegroundHook ? "开" : "关");
    }

    // ---------------- 定时扫描与重推 ----------------

    private void RestartTimer()
    {
        _timer?.Stop();
        var interval = Math.Clamp(_settings.IntervalMs, 50, 5000);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
        _timer.Tick += (_, _) => ScanAndApply();
        _timer.Start();
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TopmostSetting.IntervalMs))
        {
            RestartTimer();
        }
        else if (e.PropertyName == nameof(TopmostSetting.EnableForegroundHook))
        {
            if (_settings.EnableForegroundHook)
            {
                InstallForegroundHook();
            }
            else
            {
                UninstallForegroundHook();
            }
        }
    }

    private void ScanAndApply()
    {
        try
        {
            if (!_settings.Enabled)
            {
                return;
            }

            var application = Application.Current;
            if (application?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            {
                return;
            }

            // 收集当前所有 IslandCaller 窗口句柄。
            // 只跟踪可见窗口：被 Hide() 的窗口（点名结果窗展示结束、悬浮窗临时隐藏）
            // 不应再被周期重推打扰，否则会和业务层的 Show/Hide 竞争，
            // 导致结果窗"关不掉"或高频点名时互相踩踏卡死。
            var current = new HashSet<IntPtr>();
            foreach (var window in lifetime.Windows)
            {
                if (!window.IsVisible || !IsIslandCallerWindow(window))
                {
                    continue;
                }

                var hwnd = TryGetHwnd(window);
                if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
                {
                    continue;
                }

                current.Add(hwnd);
            }

            // 清理已关闭 / 不再匹配的窗口
            _trackedHwnds.RemoveWhere(h => !current.Contains(h) || !NativeMethods.IsWindow(h));
            foreach (var hwnd in current)
            {
                if (_trackedHwnds.Add(hwnd))
                {
                    _logger.LogInformation("发现 IslandCaller 窗口，加入最高置顶守卫：HWND=0x{Hwnd}", hwnd.ToString("X"));
                }
            }

            if (_trackedHwnds.Count == 0)
            {
                return;
            }

            // 若前台出现全屏窗口（PPT 放映 / 播放器 / 白板等），立即整体重推一次
            if (IsForegroundFullscreen())
            {
                _logger.LogTrace("检测到前台全屏窗口，立即重推置顶。");
                ApplyAllTracked();
                return;
            }

            foreach (var hwnd in _trackedHwnds)
            {
                ApplyAll(hwnd);

                // 自动恢复：置顶带内仍有其它可见窗口压在本窗口之上 → 优先级被抢占，立即再推
                if (HasVisibleWindowAbove(hwnd))
                {
                    ApplyAll(hwnd);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "扫描 / 应用置顶失败。");
        }
    }

    /// <summary>
    /// 判断当前前台窗口是否为全屏（覆盖其所在显示器的完整边界）。
    /// 全屏演示类窗口抢占焦点时，即使它们不是置顶窗口，也会触发一次整体重推，
    /// 保证 IslandCaller 窗口紧随其后回到最上层。
    /// </summary>
    private bool IsForegroundFullscreen()
    {
        try
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return false;
            }

            // 前台窗口是我们自己的目标窗口时无需处理
            if (_trackedHwnds.Contains(foreground))
            {
                return false;
            }

            // UIA 增强：前台窗口若被系统 cloaked（虚拟桌面切换 / 任务视图 / UWP 挂起），
            // 其"全屏"对用户不可见，不应触发重推，避免无谓的系统调用。
            if (_settings.EnableUiaDetection && IsCloaked(foreground))
            {
                return false;
            }

            if (!NativeMethods.GetWindowRect(foreground, out var rect))
            {
                return false;
            }

            var monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var monitorInfo = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            // 窗口矩形完全覆盖显示器边界（含任务栏区域）视为全屏。
            // 允许 2px 容差：UWP / 现代化应用在 DPI 缩放下矩形可能与边界有亚像素差异。
            return IsRectCovering(rect, monitorInfo.rcMonitor, tolerance: 2);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 检查在 Z 序上是否有其它可见窗口压在本窗口之上。
    /// 置顶窗口位于 Z 序链顶端；若上方仍存在可见窗口（通常为其它置顶窗口），
    /// 说明优先级已被抢占，需要重新置顶恢复。
    /// </summary>
    private bool HasVisibleWindowAbove(IntPtr hwnd)
    {
        for (var above = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDPREV);
             above != IntPtr.Zero;
             above = NativeMethods.GetWindow(above, NativeMethods.GW_HWNDPREV))
        {
            // 忽略隐藏 / cloaked 窗口与自身的其它目标窗口
            if (IsEffectivelyVisible(above) && !_trackedHwnds.Contains(above))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// UIA 增强：判断窗口是否"真正可见"。
    /// Win32 的 IsWindowVisible 对 cloaked 窗口（UWP 挂起、虚拟桌面切换后、
    /// 任务视图中等被 DWM 隐藏的窗口）仍返回 true，导致误判为遮挡者；
    /// 这里结合 DWM cloaked 属性（UI Automation IsOffscreen 的底层数据源）过滤，
    /// 使 Z 序校验只认"用户真正看得见的窗口"。
    /// </summary>
    private bool IsEffectivelyVisible(IntPtr hwnd)
    {
        if (!NativeMethods.IsWindowVisible(hwnd))
        {
            return false;
        }

        if (_settings.EnableUiaDetection && IsCloaked(hwnd))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 查询窗口是否被 DWM cloaked（系统级隐藏）。等价于 UI Automation 的
    /// IsOffscreen 语义，用于识别 UWP / 现代化应用的隐藏窗口。
    /// </summary>
    private static bool IsCloaked(IntPtr hwnd)
    {
        try
        {
            var result = NativeMethods.DwmGetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_CLOAKED,
                out var cloaked,
                sizeof(int));
            return result == 0 && cloaked != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>窗口矩形是否在给定容差内覆盖目标矩形（全屏判定）。</summary>
    private static bool IsRectCovering(NativeMethods.RECT rect, NativeMethods.RECT target, int tolerance)
    {
        return Math.Abs(rect.Left - target.Left) <= tolerance &&
               Math.Abs(rect.Top - target.Top) <= tolerance &&
               Math.Abs(rect.Right - target.Right) <= tolerance &&
               Math.Abs(rect.Bottom - target.Bottom) <= tolerance;
    }

    /// <summary>判断某个 Avalonia 窗口是否属于 IslandCaller。</summary>
    private bool IsIslandCallerWindow(Window window)
    {
        // 1) 程序集名匹配（跨插件隔离场景最可靠）：IslandCaller.Plugin2
        try
        {
            var assemblyName = window.GetType().Assembly.GetName().Name;
            if (assemblyName is not null &&
                assemblyName.StartsWith("IslandCaller", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch (Exception)
        {
            // 忽略反射异常，继续尝试其它匹配方式
        }

        // 2) 窗口类型名匹配（兜底）
        var typeName = window.GetType().Name;
        if (KnownWindowTypeNames.Contains(typeName))
        {
            return true;
        }

        // 3) 标题关键词匹配（用户可扩展）
        var title = window.Title;
        if (!string.IsNullOrEmpty(title) && _settings.ExtraTitleKeywords.Any(
                keyword => !string.IsNullOrEmpty(keyword) &&
                           title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static IntPtr TryGetHwnd(Window window)
    {
        try
        {
            return window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    // ---------------- 置顶应用 ----------------

    private void ApplyAll(IntPtr hwnd)
    {
        // 机制 1：置顶带 + 带内最顶端。SWP_NOACTIVATE 保证不抢焦点，
        // SWP_ASYNCWINDOWPOS 防止目标窗口线程阻塞时本调用卡死。
        // 注意：绝不能带 SWP_SHOWWINDOW —— 置顶只应改变窗口 Z 序，
        // 若带 SHOWWINDOW 会在结果窗/悬浮窗被 Hide() 后每 250ms 强制复活，
        // 造成"结果窗关不掉、抽多了界面卡死"。
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_ASYNCWINDOWPOS);

        // 机制 3：扩展样式强化（仅在需要时写入，避免无谓的系统调用）
        if (_settings.EnableTopmostStyle || _settings.EnableToolWindow || _settings.EnableNoActivate)
        {
            var style = NativeMethods.GetWindowExStyle(hwnd);
            var newStyle = style;
            if (_settings.EnableTopmostStyle)
            {
                newStyle |= NativeMethods.WS_EX_TOPMOST;
            }

            if (_settings.EnableToolWindow)
            {
                newStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            }

            if (_settings.EnableNoActivate)
            {
                newStyle |= NativeMethods.WS_EX_NOACTIVATE;
            }

            if (newStyle != style)
            {
                NativeMethods.SetWindowExStyle(hwnd, newStyle);
            }
        }
    }

    private void ApplyAllTracked()
    {
        if (!_settings.Enabled || _trackedHwnds.Count == 0)
        {
            return;
        }

        foreach (var hwnd in _trackedHwnds)
        {
            if (NativeMethods.IsWindow(hwnd))
            {
                ApplyAll(hwnd);
            }
        }
    }

    // ---------------- 前台事件钩子 ----------------

    private void InstallForegroundHook()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            return;
        }

        _winEventDelegate ??= OnWinEvent;
        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_winEventHook == IntPtr.Zero)
        {
            _logger.LogWarning("安装前台窗口事件钩子失败，错误码：{Error}", Marshal.GetLastWin32Error());
        }
        else
        {
            _logger.LogInformation("前台窗口事件钩子已安装（任何窗口抢前台都会立即触发置顶重推）。");
        }
    }

    private void UninstallForegroundHook()
    {
        if (_winEventHook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWinEvent(_winEventHook);
        _winEventHook = IntPtr.Zero;
        _logger.LogInformation("前台窗口事件钩子已卸载。");
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND || !_settings.Enabled)
        {
            return;
        }

        // 节流：事件回调频率很高，至少间隔 50ms 才真正触发一次 UI 线程重推。
        var now = Environment.TickCount64;
        if (now - _lastHookApplyTick < HookThrottleMs)
        {
            return;
        }

        _lastHookApplyTick = now;
        Dispatcher.UIThread.Post(ApplyAllTracked, DispatcherPriority.Background);
    }

    // ---------------- 生命周期 ----------------

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        _timer?.Stop();
        _timer = null;
        UninstallForegroundHook();
        _trackedHwnds.Clear();
    }
}
