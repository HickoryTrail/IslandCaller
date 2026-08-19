using Avalonia;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IslandCaller.Helpers;

/// <summary>
/// Captures a desktop rectangle and recommends a contrasting foreground color.
/// </summary>
public sealed class ScreenBrightnessHelper
{
    private const uint DIB_RGB_COLORS = 0;
    private const uint BI_RGB = 0;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;
    private const double ForegroundThreshold = 0.179;
    private static readonly double[] SrgbToLinearLookup = CreateSrgbToLinearLookup();

    private readonly ILogger<ScreenBrightnessHelper> logger;

    public ScreenBrightnessHelper(ILogger<ScreenBrightnessHelper> logger)
    {
        this.logger = logger;
    }

    public bool TryGetAverageRelativeLuminance(PixelRect screenRect, out double luminance)
    {
        luminance = 0;
        if (!OperatingSystem.IsWindows() || screenRect.Width <= 0 || screenRect.Height <= 0)
        {
            return false;
        }

        var pixelCount = (long)screenRect.Width * screenRect.Height;
        var byteCount = pixelCount * 4;
        if (pixelCount > 16_000_000 || byteCount > int.MaxValue)
        {
            logger.LogWarning("屏幕捕获区域过大，已跳过亮度计算: {Rect}", screenRect);
            return false;
        }

        var stopwatch = Stopwatch.StartNew();
        var screenDc = IntPtr.Zero;
        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousBitmap = IntPtr.Zero;

        try
        {
            screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                return LogCaptureFailure("GetDC");
            }

            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                return LogCaptureFailure("CreateCompatibleDC");
            }

            var bitmapInfo = new BITMAPINFO
            {
                Header = new BITMAPINFOHEADER
                {
                    Size = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    Width = screenRect.Width,
                    Height = -screenRect.Height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BI_RGB
                }
            };

            bitmap = CreateDIBSection(memoryDc, ref bitmapInfo, DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
            {
                return LogCaptureFailure("CreateDIBSection");
            }

            previousBitmap = SelectObject(memoryDc, bitmap);
            if (previousBitmap == IntPtr.Zero)
            {
                return LogCaptureFailure("SelectObject");
            }

            if (!BitBlt(
                    memoryDc,
                    0,
                    0,
                    screenRect.Width,
                    screenRect.Height,
                    screenDc,
                    screenRect.X,
                    screenRect.Y,
                    SRCCOPY | CAPTUREBLT))
            {
                return LogCaptureFailure("BitBlt");
            }

            var pixels = new byte[(int)byteCount];
            Marshal.Copy(bits, pixels, 0, pixels.Length);

            double totalLuminance = 0;
            for (var index = 0; index < pixels.Length; index += 4)
            {
                totalLuminance +=
                    0.2126 * SrgbToLinearLookup[pixels[index + 2]] +
                    0.7152 * SrgbToLinearLookup[pixels[index + 1]] +
                    0.0722 * SrgbToLinearLookup[pixels[index]];
            }

            luminance = totalLuminance / pixelCount;
            stopwatch.Stop();
            logger.LogTrace("已计算屏幕区域亮度: {Rect}, Luminance={Luminance:F3}, 耗时 {ElapsedMs} ms", screenRect, luminance, stopwatch.Elapsed.TotalMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "屏幕捕获亮度计算失败: {Rect}", screenRect);
            return false;
        }
        finally
        {
            if (memoryDc != IntPtr.Zero && previousBitmap != IntPtr.Zero)
            {
                SelectObject(memoryDc, previousBitmap);
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            if (screenDc != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    public static Color GetRecommendedForeground(double luminance)
    {
        return luminance < ForegroundThreshold ? Colors.White : Colors.Black;
    }

    private bool LogCaptureFailure(string operation)
    {
        var errorCode = Marshal.GetLastWin32Error();
        logger.LogWarning("{Operation} 调用失败，错误码: {ErrorCode}", operation, errorCode);
        return false;
    }

    private static double ToLinear(double value)
    {
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double[] CreateSrgbToLinearLookup()
    {
        var lookup = new double[256];
        for (var value = 0; value < lookup.Length; value++)
        {
            lookup[value] = ToLinear(value / 255d);
        }

        return lookup;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BITMAPINFO pbmi,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        IntPtr hdc,
        int x,
        int y,
        int width,
        int height,
        IntPtr sourceHdc,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER Header;
        public RGBQUAD Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RGBQUAD
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Reserved;
    }
}
