using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace IslandCaller.Helpers
{
    public class WindowDragHelper
    {
        public ILogger<WindowDragHelper> logger = IAppHost.GetService<ILogger<WindowDragHelper>>();
        private bool _isDragging;
        private Point _dragStartPoint;
        private PixelPoint _windowStartPosition;

        // --- Win32 API 导入 ---
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterTouchWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MOVE = 0xF010;
        private const int HTCAPTION = 0x0002;
        private const int ERROR_INVALID_PARAMETER = 87;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly object TouchDisableLock = new();
        private static readonly HashSet<IntPtr> TouchDisabledWindows = new();

        public async Task DragMoveAsync(Window window, PointerType pointerType)
        {
            // 检查是否为 Windows 系统
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await DragMoveWindowsAsync(window, pointerType);
            }
            else
            {
                // Linux: manual drag is handled via StartDrag/OnDrag/StopDrag
                await Task.CompletedTask;
            }
        }

        public void StartDrag(Window window, PointerPressedEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(window);
            _windowStartPosition = window.Position;
            e.Pointer.Capture((IInputElement)e.Source!);
        }

        public void OnDrag(Window window, PointerEventArgs e)
        {
            if (!_isDragging) return;
            var currentPoint = e.GetPosition(window);
            var diff = currentPoint - _dragStartPoint;
            window.Position = new PixelPoint(
                _windowStartPosition.X + (int)diff.X,
                _windowStartPosition.Y + (int)diff.Y);
        }

        public void StopDrag(PointerReleasedEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            e.Pointer.Capture(null);
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private async Task DragMoveWindowsAsync(Window window, PointerType pointerType)
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null) return;

            IntPtr hwnd = platformHandle.Handle;

            if (pointerType == PointerType.Touch || pointerType == PointerType.Pen)
                EnsureTouchInputDisabled(hwnd);

            ReleaseCapture();
            await Task.Run(() => { SendMessage(hwnd, WM_SYSCOMMAND, SC_MOVE + HTCAPTION, 0); });
        }

        public bool SetWindowPosition(Window window, PixelPoint position)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                window.Position = position;
                return true;
            }

            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null) return false;

            var hwnd = platformHandle.Handle;
            return SetWindowPos(hwnd, IntPtr.Zero, position.X, position.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void EnsureTouchInputDisabled(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            lock (TouchDisableLock)
            {
                if (TouchDisabledWindows.Contains(hwnd)) return;
                TouchDisabledWindows.Add(hwnd);
            }

            if (!UnregisterTouchWindow(hwnd))
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode == ERROR_INVALID_PARAMETER) return;
                lock (TouchDisableLock) { TouchDisabledWindows.Remove(hwnd); }
            }
        }
    }
}