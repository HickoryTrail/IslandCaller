using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ClassIsland.Core.Controls;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

internal class WindowsManager
{
    private readonly ILogger<WindowsManager> _logger;
    private readonly ScreenBrightnessHelper _screenBrightnessHelper;
    private readonly LiquidGlassRuntime _liquidGlassRuntime;
    private bool _isInitialized;
    private bool _isRecreatingHover;

    public Window? HoverWindow { get; private set; }
    public Window? ShowerWindow { get; private set; }

    public WindowsManager(
        ILogger<WindowsManager> logger,
        ScreenBrightnessHelper screenBrightnessHelper,
        LiquidGlassRuntime liquidGlassRuntime)
    {
        _logger = logger;
        _screenBrightnessHelper = screenBrightnessHelper;
        _liquidGlassRuntime = liquidGlassRuntime;
        _logger.LogTrace("WindowsManager created.");
    }

    internal void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        Settings.Instance.Hover.PropertyChanged += OnHoverSettingChanged;
        if (Settings.Instance.Hover.IsEnable)
        {
            ShowHoverWindow();
        }

        _logger.LogInformation("WindowsManager initialized.");
    }

    internal async Task ShowCallWindowAsync(string text, float duration, CancellationToken token)
    {
        _logger.LogInformation("Showing call window for {Duration} seconds with text: {Text}", duration, text);
        var icon = new FluentIcon
        {
            Glyph = "\uECF8",
            FontSize = 60,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var nameText = new TextBlock
        {
            Text = text,
            FontSize = 60,
            FontWeight = FontWeight.Bold,
            FontStretch = FontStretch.Expanded,
            Margin = new Thickness(15, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var showPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(25, 0),
            Children = { icon, nameText }
        };

        var showerWindow = CreateShowerWindow();
        showerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        showerWindow.SizeToContent = SizeToContent.Manual;
        ShowerWindow = showerWindow;

        if (showerWindow is LiquidShower liquidShower)
        {
            liquidShower.SetDisplayContent(showPanel);
        }
        else
        {
            showerWindow.Content = showPanel;
        }

        showPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var screen = showerWindow.Screens.Primary;
        PixelRect? captureRect = null;
        if (screen is not null && showPanel.DesiredSize.Width > 0)
        {
            var scaling = screen.Scaling;
            var width = Math.Max(1, (int)Math.Ceiling(showPanel.DesiredSize.Width));
            var height = Math.Max(1, (int)Math.Ceiling(showerWindow.Height));
            showerWindow.Width = width;

            var widthPixels = Math.Max(1, (int)Math.Ceiling(width * scaling));
            var heightPixels = Math.Max(1, (int)Math.Ceiling(height * scaling));
            var workArea = screen.WorkingArea;
            var x = workArea.X + Math.Max(0, (workArea.Width - widthPixels) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - heightPixels) / 2);
            showerWindow.Position = new PixelPoint(x, y);
            captureRect = new PixelRect(x, y, widthPixels, heightPixels);
        }

        if (showerWindow is FluentShower)
        {
            var foreground = Brushes.Black;
            if (captureRect is PixelRect rect &&
                _screenBrightnessHelper.TryGetAverageRelativeLuminance(rect, out var luminance))
            {
                foreground = ScreenBrightnessHelper.GetRecommendedForeground(luminance) == Colors.White
                    ? Brushes.White
                    : Brushes.Black;
            }

            icon.Foreground = foreground;
            nameText.Foreground = foreground;
        }

        showerWindow.Show();
        try
        {
            await Task.Delay((int)(duration * 1000), token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            showerWindow.Close();
            if (ReferenceEquals(ShowerWindow, showerWindow))
            {
                ShowerWindow = null;
            }
        }

        _logger.LogInformation("Call window closed for text: {Text}", text);
    }

    internal void ShowCallWindow(string text, float duration, CancellationToken token) => _ = ShowCallWindowAsync(text, duration, token);

    internal void ShowHoverWindow()
    {
        HoverWindow ??= CreateHoverWindow();
        HoverWindow.Show();
        _logger.LogInformation("Hover window shown: {Theme}", HoverWindow.GetType().Name);
    }

    internal void HideHoverWindow()
    {
        HoverWindow?.Hide();
        _logger.LogInformation("Hover window hidden.");
    }

    internal void CloseHoverWindow()
    {
        HoverWindow?.Close();
        HoverWindow = null;
        _logger.LogInformation("Hover window closed.");
    }

    private Window CreateHoverWindow() => _liquidGlassRuntime.CanUseHoverTheme()
        ? new HoverLiquid()
        : new HoverFluent();

    private Window CreateShowerWindow() => _liquidGlassRuntime.CanUseShowerTheme()
        ? new LiquidShower()
        : new FluentShower();

    private void OnHoverSettingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HoverSetting.HoverTheme) || _isRecreatingHover || HoverWindow is null)
        {
            return;
        }

        _isRecreatingHover = true;
        try
        {
            bool wasVisible = HoverWindow.IsVisible;
            CloseHoverWindow();
            if (wasVisible)
            {
                ShowHoverWindow();
            }
        }
        finally
        {
            _isRecreatingHover = false;
        }
    }
}
