using Avalonia.Controls;
using IslandCaller.Models;
using System.Runtime.InteropServices;

namespace IslandCaller.Helpers;

public sealed class WindowContextMenuSuppressor : IDisposable
{
    private const uint WmContextMenu = 0x007B;
    private const uint WmTabletQuerySystemGestureStatus = 0x02CC;
    private const int TabletDisablePressAndHold = 0x00000001;
    private const int TabletDisablePenTapFeedback = 0x00000008;
    private const int TabletDisablePenBarrelFeedback = 0x00000010;
    private const int TabletDisableFlicks = 0x00010000;
    private const int DisabledTabletGestures = TabletDisablePressAndHold | TabletDisablePenTapFeedback
        | TabletDisablePenBarrelFeedback | TabletDisableFlicks;
    private const string TabletPenServiceProperty = "MicrosoftTabletPenServiceProperty";
    private const uint FeedbackPenPressAndHold = 5;
    private const uint FeedbackTouchPressAndHold = 9;
    private const uint GcAllGestures = 0x00000001;
    private readonly Window _window;
    private readonly Win32Properties.CustomWndProcHookCallback _hook;
    private IntPtr _configuredHwnd;
    private IntPtr _previousTabletProperty;
    private GestureConfig[]? _previousGestureConfig;
    private bool _isMiniLayout;

    [StructLayout(LayoutKind.Sequential)]
    private struct GestureConfig
    {
        public uint Id;
        public uint Want;
        public uint Block;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGestureConfig(IntPtr hwnd, uint reserved, uint flags, ref uint count, [In, Out] GestureConfig[] config, uint size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetGestureConfig(IntPtr hwnd, uint reserved, uint count, [In] GestureConfig[] config, uint size);

    [DllImport("user32.dll", EntryPoint = "GetPropW", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetProp(IntPtr hwnd, string name);

    [DllImport("user32.dll", EntryPoint = "SetPropW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProp(IntPtr hwnd, string name, IntPtr data);

    [DllImport("user32.dll", EntryPoint = "RemovePropW", CharSet = CharSet.Unicode)]
    private static extern IntPtr RemoveProp(IntPtr hwnd, string name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowFeedbackSetting(IntPtr hwnd, uint feedback, uint flags, uint size, in int configuration);

    [DllImport("user32.dll", EntryPoint = "SetWindowFeedbackSetting")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ResetWindowFeedbackSetting(IntPtr hwnd, uint feedback, uint flags, uint size, IntPtr configuration);

    public WindowContextMenuSuppressor(Window window)
    {
        _window = window;
        _hook = OnWndProc;
        Win32Properties.AddWndProcHookCallback(_window, _hook);
        UpdateLayout(Settings.Instance.Hover.HoverLayout);
    }

    public void UpdateLayout(int hoverLayout)
    {
        _isMiniLayout = hoverLayout == 2;
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!_isMiniLayout)
        {
            RestoreWindowSettings();
            return;
        }

        var hwnd = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero || hwnd == _configuredHwnd)
        {
            return;
        }

        RestoreWindowSettings();
        _configuredHwnd = hwnd;
        _previousTabletProperty = GetProp(hwnd, TabletPenServiceProperty);
        // GetGestureConfig accepts individual GIDs, not the global ID 0 used
        // by SetGestureConfig. Snapshot them all so layout changes can restore them.
        GestureConfig[] previousGestures =
        [
            new() { Id = 3 }, // GID_ZOOM
            new() { Id = 4 }, // GID_PAN
            new() { Id = 5 }, // GID_ROTATE
            new() { Id = 6 }, // GID_TWOFINGERTAP
            new() { Id = 7 }  // GID_PRESSANDTAP
        ];
        uint count = (uint)previousGestures.Length;
        uint gestureConfigSize = (uint)Marshal.SizeOf<GestureConfig>();
        if (GetGestureConfig(hwnd, 0, 0, ref count, previousGestures, gestureConfigSize))
        {
            _previousGestureConfig = previousGestures;
            SetGestureConfig(hwnd, 0, 1, [new() { Block = GcAllGestures }], gestureConfigSize);
        }
        // Disable the OS right-click recognizer before it delays/promotes touch
        // input. Swallowing WM_CONTEXTMENU alone is too late to hide its square.
        SetProp(hwnd, TabletPenServiceProperty,
            new IntPtr(_previousTabletProperty.ToInt64() | DisabledTabletGestures));
        int disabled = 0;
        SetWindowFeedbackSetting(hwnd, FeedbackTouchPressAndHold, 0, sizeof(int), in disabled);
        SetWindowFeedbackSetting(hwnd, FeedbackPenPressAndHold, 0, sizeof(int), in disabled);
    }

    private IntPtr OnWndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_isMiniLayout)
        {
            return IntPtr.Zero;
        }

        if (message == WmTabletQuerySystemGestureStatus)
        {
            handled = true;
            return new IntPtr(DisabledTabletGestures);
        }

        if (message == WmContextMenu)
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void RestoreWindowSettings()
    {
        if (_configuredHwnd == IntPtr.Zero)
        {
            return;
        }

        if (_previousTabletProperty == IntPtr.Zero)
        {
            RemoveProp(_configuredHwnd, TabletPenServiceProperty);
        }
        else
        {
            SetProp(_configuredHwnd, TabletPenServiceProperty, _previousTabletProperty);
        }

        ResetWindowFeedbackSetting(_configuredHwnd, FeedbackTouchPressAndHold, 0, 0, IntPtr.Zero);
        ResetWindowFeedbackSetting(_configuredHwnd, FeedbackPenPressAndHold, 0, 0, IntPtr.Zero);
        if (_previousGestureConfig is not null)
        {
            SetGestureConfig(_configuredHwnd, 0, (uint)_previousGestureConfig.Length, _previousGestureConfig,
                (uint)Marshal.SizeOf<GestureConfig>());
            _previousGestureConfig = null;
        }
        _configuredHwnd = IntPtr.Zero;
    }

    public void Dispose()
    {
        Win32Properties.RemoveWndProcHookCallback(_window, _hook);
        RestoreWindowSettings();
    }
}
