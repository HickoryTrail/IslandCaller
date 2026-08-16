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

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    // Avalonia handles WM_WINDOWPOSCHANGING and may reapply its content
    // minimum. Suppress that message for this native size operation.
    private const uint SWP_NOSENDCHANGING = 0x0400;
    private const uint WM_GETMINMAXINFO = 0x0024;
    private const uint WM_WINDOWPOSCHANGING = 0x0046;
    private static readonly object CompactSizeGuardsLock = new();
    private static readonly Dictionary<IntPtr, PixelSize> CompactSizeGuards = new();
    private static readonly HashSet<IntPtr> SubclassedCompactSizeWindows = new();
    private static readonly UIntPtr CompactSizeGuardId = new(1);
    private static readonly SUBCLASSPROC CompactSizeGuardProc = CompactSizeGuardWindowProc;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SUBCLASSPROC(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr Hwnd;
        public IntPtr HwndInsertAfter;
        public int X;
        public int Y;
        public int Cx;
        public int Cy;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT Reserved;
        public POINT MaxSize;
        public POINT MaxPosition;
        public POINT MinTrackSize;
        public POINT MaxTrackSize;
    }

    private readonly record struct PixelSize(int Width, int Height);

    /// <summary>
    /// Keeps a compact HWND size authoritative when Avalonia processes native
    /// move messages and attempts to restore its content-derived minimum.
    /// </summary>
    public bool UpdateCompactSizeGuard(Window window, double width, double height)
    {
        if (width <= 0 || height <= 0 || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            return false;
        }

        var targetSize = new PixelSize(
            Math.Max(1, (int)Math.Round(width * window.RenderScaling)),
            Math.Max(1, (int)Math.Round(height * window.RenderScaling)));

        lock (CompactSizeGuardsLock)
        {
            CompactSizeGuards[platformHandle.Handle] = targetSize;
            if (SubclassedCompactSizeWindows.Contains(platformHandle.Handle))
            {
                return true;
            }

            if (SetWindowSubclass(platformHandle.Handle, CompactSizeGuardProc, CompactSizeGuardId, UIntPtr.Zero))
            {
                SubclassedCompactSizeWindows.Add(platformHandle.Handle);
                return true;
            }

            CompactSizeGuards.Remove(platformHandle.Handle);
            logger.LogWarning(
                "无法安装紧凑窗口尺寸保护，错误码: {ErrorCode}",
                Marshal.GetLastWin32Error());
            return false;
        }
    }

    /// <summary>
    /// Removes the native compact-size guard before the HWND is destroyed.
    /// </summary>
    public void RemoveCompactSizeGuard(Window window)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            return;
        }

        lock (CompactSizeGuardsLock)
        {
            CompactSizeGuards.Remove(platformHandle.Handle);
            if (SubclassedCompactSizeWindows.Remove(platformHandle.Handle))
            {
                RemoveWindowSubclass(platformHandle.Handle, CompactSizeGuardProc, CompactSizeGuardId);
            }
        }
    }

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

        // PositionChanged is raised for every native move. Sending a size
        // request when the HWND is already correct causes Avalonia's layout
        // minimum and the compact target size to continually fight each other.
        if (GetWindowRect(platformHandle.Handle, out var currentBounds)
            && currentBounds.Width == widthPixels
            && currentBounds.Height == heightPixels)
        {
            return true;
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

    private static IntPtr CompactSizeGuardWindowProc(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData)
    {
        var result = DefSubclassProc(hWnd, message, wParam, lParam);

        PixelSize targetSize;
        lock (CompactSizeGuardsLock)
        {
            if (!CompactSizeGuards.TryGetValue(hWnd, out targetSize))
            {
                return result;
            }
        }

        // Run after Avalonia's WndProc so its content-derived minimum cannot
        // replace the compact dimensions before Windows commits the move.
        switch (message)
        {
            case WM_WINDOWPOSCHANGING when lParam != IntPtr.Zero:
            {
                var windowPosition = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                if ((windowPosition.Flags & SWP_NOSIZE) == 0)
                {
                    windowPosition.Cx = targetSize.Width;
                    windowPosition.Cy = targetSize.Height;
                    Marshal.StructureToPtr(windowPosition, lParam, false);
                }

                break;
            }
            case WM_GETMINMAXINFO when lParam != IntPtr.Zero:
            {
                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.MinTrackSize.X = targetSize.Width;
                minMaxInfo.MinTrackSize.Y = targetSize.Height;
                Marshal.StructureToPtr(minMaxInfo, lParam, false);
                break;
            }
        }

        return result;
    }
}
