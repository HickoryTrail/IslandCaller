using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Enums;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace IslandCaller.Helpers
{
    public class WindowTopmostHelper
    {
        private readonly ILogger<WindowTopmostHelper> logger = ClassIsland.Shared.IAppHost.GetService<ILogger<WindowTopmostHelper>>();
        private readonly HashSet<IntPtr> _initializedWindows = new();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XOpenDisplay(string display);

        [DllImport("libX11.so.6")]
        private static extern int XRaiseWindow(IntPtr display, IntPtr window);

        [DllImport("libX11.so.6")]
        private static extern int XFlush(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(IntPtr display);

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_NOACTIVATE = 0x08000000L;

        public void EnsureTopmost(Window window)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                EnsureTopmostWindows(window);
            else
                EnsureTopmostLinux(window);
        }

        private void EnsureTopmostLinux(Window window)
        {
            try
            {
                var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle == IntPtr.Zero) { window.Topmost = true; return; }

                var svc = PlatformServices.WindowPlatformService;

                if (!_initializedWindows.Contains(handle))
                {
                    svc.SetWindowFeature(window, WindowFeatures.ToolWindow, true);
                    svc.SetWindowFeature(window, WindowFeatures.SkipManagement, true);
                    _initializedWindows.Add(handle);
                    logger.LogInformation("Linux:SkipManagement+ToolWindow set for window {Handle}", handle);
                }

                var display = XOpenDisplay(null);
                if (display != IntPtr.Zero)
                {
                    XRaiseWindow(display, handle);
                    XFlush(display);
                    XCloseDisplay(display);
                }

                window.Topmost = true;
            }
            catch (Exception ex)
            {
                window.Topmost = true;
                logger.LogDebug("Linux topmost fallback: {Message}", ex.Message);
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private void EnsureTopmostWindows(Window window)
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null) return;
            SetWindowPos(platformHandle.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void EnsureNoActivate(Window window)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            EnsureNoActivateWindows(window);
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private void EnsureNoActivateWindows(Window window)
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null) return;
            var hwnd = platformHandle.Handle;
            var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle.ToInt64() | WS_EX_NOACTIVATE));
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
            IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, (int)dwNewLong.ToInt64()));
    }
}