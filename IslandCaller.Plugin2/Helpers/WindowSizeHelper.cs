using Avalonia;
using Avalonia.Controls;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace IslandCaller.Helpers;

/// <summary>
/// Sets a window's native pixel size without going through Avalonia's
/// Width/Height coercion. This is needed by the compact Hover window, whose
/// size can legitimately be smaller than Avalonia's content-derived minimum.
/// </summary>
public sealed class WindowSizeHelper
{
    private readonly ILogger<WindowSizeHelper> logger =
        IAppHost.GetService<ILogger<WindowSizeHelper>>();

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    // Avalonia handles WM_WINDOWPOSCHANGING and may reapply its content
    // minimum. Suppress that message for this native size operation.
    private const uint SWP_NOSENDCHANGING = 0x0400;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    /// <summary>
    /// Applies a size expressed in Avalonia DIPs as native Windows pixels.
    /// </summary>
    public bool SetWindowSize(Window window, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var scaling = window.RenderScaling;
        var widthPixels = Math.Max(1, (int)Math.Round(width * scaling));
        var heightPixels = Math.Max(1, (int)Math.Round(height * scaling));

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Keep the helper usable in design/tests on other platforms.
            window.Width = width;
            window.Height = height;
            return true;
        }

        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            logger.LogWarning("无法获取窗口句柄，设置窗口大小失败。");
            return false;
        }

        var success = SetWindowPos(
            platformHandle.Handle,
            IntPtr.Zero,
            0,
            0,
            widthPixels,
            heightPixels,
            SWP_NOMOVE
                | SWP_NOZORDER
                | SWP_NOACTIVATE
                | SWP_NOOWNERZORDER
                | SWP_NOSENDCHANGING);

        if (!success)
        {
            logger.LogWarning(
                "SetWindowPos 设置窗口大小失败，错误码: {ErrorCode}",
                Marshal.GetLastWin32Error());
        }

        return success;
    }
}
