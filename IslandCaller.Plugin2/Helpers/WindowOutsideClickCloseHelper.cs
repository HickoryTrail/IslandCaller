using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace IslandCaller.Helpers;

internal sealed class WindowOutsideClickCloseHelper : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int HC_ACTION = 0;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;

    private readonly Window targetWindow;
    private readonly Action closeAction;
    private readonly ILogger<WindowOutsideClickCloseHelper> logger = IAppHost.GetService<ILogger<WindowOutsideClickCloseHelper>>();
    private readonly LowLevelMouseProc hookProc;
    private IntPtr hookHandle;
    private bool disposed;

    public WindowOutsideClickCloseHelper(Window targetWindow, Action closeAction)
    {
        this.targetWindow = targetWindow;
        this.closeAction = closeAction;
        hookProc = HookCallback;
    }

    public void Start()
    {
        if (disposed || hookHandle != IntPtr.Zero || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        hookHandle = SetWindowsHookEx(WH_MOUSE_LL, hookProc, IntPtr.Zero, 0);
        if (hookHandle == IntPtr.Zero)
        {
            var errorCode = Marshal.GetLastWin32Error();
            logger.LogWarning("安装全局鼠标钩子失败，错误码: {ErrorCode}", errorCode);
            return;
        }

        logger.LogTrace("已为 PersonalCall 启动点外关闭监听。");
    }

    public void Stop()
    {
        if (hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (!UnhookWindowsHookEx(hookHandle))
        {
            var errorCode = Marshal.GetLastWin32Error();
            logger.LogWarning("卸载全局鼠标钩子失败，错误码: {ErrorCode}", errorCode);
        }

        hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= HC_ACTION && IsMouseDownMessage(wParam))
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if (ShouldCloseForPoint(hookStruct.pt))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!disposed && targetWindow.IsVisible)
                    {
                        closeAction();
                    }
                });
            }
        }

        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private bool ShouldCloseForPoint(POINT point)
    {
        if (!targetWindow.IsVisible)
        {
            return false;
        }

        var scaling = targetWindow.RenderScaling;
        var width = (int)Math.Round(targetWindow.Bounds.Width * scaling);
        var height = (int)Math.Round(targetWindow.Bounds.Height * scaling);

        if (width <= 0)
        {
            width = (int)Math.Round(targetWindow.Width * scaling);
        }

        if (height <= 0)
        {
            height = (int)Math.Round(targetWindow.Height * scaling);
        }

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var bounds = new PixelRect(targetWindow.Position.X, targetWindow.Position.Y, width, height);
        return !bounds.Contains(new PixelPoint(point.X, point.Y));
    }

    private static bool IsMouseDownMessage(IntPtr wParam)
    {
        var message = unchecked((int)wParam.ToInt64());
        return message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
