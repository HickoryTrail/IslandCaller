using System.Runtime.InteropServices;
using Avalonia.Threading;
using ClassIsland.Core.Controls;
using ClassIsland.Shared;
using IslandCaller.Helpers;
using IslandCaller.Models;
using Microsoft.Extensions.Logging;
// 命名空间 IslandCaller.Services.IslandCallerService 与同名类型冲突，这里用别名明确指向类型。
using IslandCallerServiceImpl = IslandCaller.Services.IslandCallerService.IslandCallerService;

namespace IslandCaller.Services;

/// <summary>全局快捷键的注册状态。</summary>
public enum HotkeyRegistrationState
{
    /// <summary>未启用。</summary>
    Disabled,

    /// <summary>已成功注册并生效。</summary>
    Registered,

    /// <summary>组合已被其它程序占用（ERROR_HOTKEY_ALREADY_REGISTERED）。</summary>
    Conflict,

    /// <summary>其它注册失败。</summary>
    Failed,

    /// <summary>设置本身不合法（缺少修饰键或主键不受支持）。</summary>
    Invalid
}

/// <summary>
/// 全局快捷键点名。
/// 通过 Win32 RegisterHotKey 在一条专用后台线程上注册（hWnd = NULL，WM_HOTKEY 投递到该线程消息队列），
/// 因此不依赖 ClassIsland 或悬浮窗是否存在窗口。触发后统一调度回 UI 线程，复用
/// <see cref="IslandCallerImpl.ShowRandomStudent(int)"/>，与悬浮窗「Call」按钮走同一名单与同一权重算法。
/// </summary>
public sealed class HotkeyService : IDisposable
{
    /// <summary>本插件占用的热键 ID（"CL"）。</summary>
    private const int HotkeyId = 0x434C;

    /// <summary>重复触发去抖窗口（毫秒）：窗口内的连续触发被忽略；长按由 MOD_NOREPEAT 兜底。</summary>
    private const int DebounceMs = 500;

    private const string ThreadName = "IslandCaller.Hotkey";

    private readonly ILogger<HotkeyService> _logger;
    private readonly object _sync = new();
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _running;
    private long _lastTriggerTick;
    private bool _disposed;

    public HotkeyService(ILogger<HotkeyService> logger)
    {
        _logger = logger;
    }

    /// <summary>当前注册状态。</summary>
    public HotkeyRegistrationState State { get; private set; } = HotkeyRegistrationState.Disabled;

    /// <summary>状态的可读说明（用于设置页提示与冲突反馈）。</summary>
    public string StateMessage { get; private set; } = "未启用";

    /// <summary>状态变化通知。</summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// 按当前设置重新注册快捷键。设置变更（保存）或应用启动时调用。
    /// </summary>
    /// <param name="silent">true 时不弹出注册失败对话框，由调用方自行反馈（设置页使用）。</param>
    public void Apply(bool silent = false)
    {
        if (_disposed)
        {
            return;
        }

        var setting = Settings.Instance.Hotkey;

        // 先停掉旧注册，避免叠加占用。
        Stop();

        if (!setting.Enabled)
        {
            SetState(HotkeyRegistrationState.Disabled, "未启用");
            _logger.LogInformation("快捷键点名未启用。");
            return;
        }

        if (!HotkeyText.Validate(setting.Modifiers, setting.Key, out string error))
        {
            SetState(HotkeyRegistrationState.Invalid, error);
            _logger.LogWarning("快捷键设置不合法：{Error}", error);
            if (!silent)
            {
                ShowFeedback("快捷键不可用", error);
            }

            return;
        }

        if (!HotkeyKeyMap.TryGetVirtualKey(setting.Key, out uint virtualKey))
        {
            SetState(HotkeyRegistrationState.Invalid, $"不支持的主键：{setting.Key}");
            return;
        }

        Start(virtualKey, ToWin32Modifiers(setting.Modifiers), silent);
    }

    private void Start(uint virtualKey, uint modifiers, bool silent)
    {
        // 注册结果在专用线程写入，主线程通过 ManualResetEventSlim 等待后读取（含内存屏障）。
        var ready = new ManualResetEventSlim(false);
        var resultState = HotkeyRegistrationState.Failed;
        var resultMessage = "注册失败";
        uint resultError = 0;

        _running = true;
        var thread = new Thread(() =>
        {
            _threadId = NativeMethods.GetCurrentThreadId();

            // 先创建线程消息队列，确保其它线程的 PostThreadMessage 不会失败。
            NativeMethods.PeekMessage(out _, IntPtr.Zero, 0, 0, NativeMethods.PM_NOREMOVE);

            bool ok = NativeMethods.RegisterHotKey(
                IntPtr.Zero,
                HotkeyId,
                modifiers | NativeMethods.MOD_NOREPEAT,
                virtualKey);

            if (ok)
            {
                resultState = HotkeyRegistrationState.Registered;
                resultMessage = "已生效";
            }
            else
            {
                resultError = (uint)Marshal.GetLastWin32Error();
                if (resultError == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED)
                {
                    resultState = HotkeyRegistrationState.Conflict;
                    resultMessage = "该组合已被其它程序占用";
                }
                else
                {
                    resultState = HotkeyRegistrationState.Failed;
                    resultMessage = $"注册失败（错误码 {resultError}）";
                }
            }

            ready.Set();

            if (!ok)
            {
                return;
            }

            while (_running && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == NativeMethods.WM_HOTKEY)
                {
                    OnHotkeyPressed();
                }
            }

            // 必须在注册它的同一线程上注销。
            NativeMethods.UnregisterHotKey(IntPtr.Zero, HotkeyId);
        })
        {
            IsBackground = true,
            Name = ThreadName
        };

        lock (_sync)
        {
            _thread = thread;
            thread.Start();
        }

        if (!ready.Wait(TimeSpan.FromSeconds(3)))
        {
            _logger.LogError("快捷键注册等待超时。");
            Stop();
            SetState(HotkeyRegistrationState.Failed, "注册超时");
            if (!silent)
            {
                ShowFeedback("快捷键不可用", "快捷键注册等待超时，请重试或在设置中更换组合。");
            }

            return;
        }

        SetState(resultState, resultMessage);

        string gesture = Settings.Instance.Hotkey.GestureText;
        if (resultState == HotkeyRegistrationState.Registered)
        {
            _logger.LogInformation("快捷键已注册：{Gesture}（范围：{Scope}）。", gesture, Settings.Instance.Hotkey.Scope);
            return;
        }

        _logger.LogWarning("快捷键 {Gesture} 注册失败：{Message}（错误码 {Error}）。", gesture, resultMessage, resultError);
        if (!silent)
        {
            string hint = resultState == HotkeyRegistrationState.Conflict
                ? $"{gesture} 已被其它程序占用，请在「快捷键点名」中更换组合。"
                : $"{gesture} 注册失败：{resultMessage}。";
            ShowFeedback("快捷键不可用", hint);
        }
    }

    private void OnHotkeyPressed()
    {
        var setting = Settings.Instance.Hotkey;
        if (!setting.Enabled)
        {
            return;
        }

        // 重复触发去抖：窗口内的连续触发直接忽略；长按由 MOD_NOREPEAT 兜底。
        long now = Environment.TickCount64;
        if (now - _lastTriggerTick < DebounceMs)
        {
            _logger.LogTrace("快捷键触发被去抖忽略（间隔 {Interval}ms）。", now - _lastTriggerTick);
            return;
        }

        // 触发范围：仅 ClassIsland 前台时，校验当前前台窗口所属进程。
        if (setting.Scope == HotkeyScope.ClassIslandForeground && !IsClassIslandForeground())
        {
            _logger.LogDebug("快捷键触发被忽略：ClassIsland 不在前台。");
            return;
        }

        _lastTriggerTick = now;
        _logger.LogInformation("快捷键 {Gesture} 触发点名（范围：{Scope}）。", setting.GestureText, setting.Scope);

        // 抽选涉及窗口/通知，必须回到 UI 线程执行（与点击悬浮窗按钮等价）。
        Dispatcher.UIThread.Post(TriggerOnUiThread);
    }

    private void TriggerOnUiThread()
    {
        try
        {
            IAppHost.GetService<IslandCallerServiceImpl>().TriggerRandomCallFromHotkey();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "快捷键触发点名时发生异常。");
        }
    }

    private static bool IsClassIslandForeground()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        // 插件运行在 ClassIsland 进程内，进程一致即视为 ClassIsland（含本插件悬浮窗）位于前台。
        return pid == (uint)Environment.ProcessId;
    }

    private static uint ToWin32Modifiers(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) result |= NativeMethods.MOD_WIN;
        return result;
    }

    private void SetState(HotkeyRegistrationState state, string message)
    {
        State = state;
        StateMessage = message;
        try
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "快捷键状态通知处理失败。");
        }
    }

    private void ShowFeedback(string header, string content)
    {
        void Show() => _ = CommonTaskDialogs.ShowDialog(header, content);
        if (Dispatcher.UIThread.CheckAccess())
        {
            Show();
        }
        else
        {
            Dispatcher.UIThread.Post(Show);
        }
    }

    /// <summary>停止并注销当前快捷键（可重复调用）。</summary>
    public void Stop()
    {
        Thread? thread;
        uint threadId;
        lock (_sync)
        {
            _running = false;
            thread = _thread;
            threadId = _threadId;
            _thread = null;
            _threadId = 0;
        }

        if (thread is null)
        {
            return;
        }

        if (threadId != 0)
        {
            // 通过线程消息队列投递 WM_QUIT，使 GetMessage 返回 0 并走注销流程。
            NativeMethods.PostThreadMessage(threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        if (!thread.Join(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("快捷键线程未能在超时内退出。");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
