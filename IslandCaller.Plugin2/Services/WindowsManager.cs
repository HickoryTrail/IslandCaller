using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ClassIsland.Core.Controls;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Plugin2.Services
{
    internal class WindowsManager
    {
        private ILogger<WindowsManager>? Logger { get; set; }
        private readonly ScreenBrightnessHelper screenBrightnessHelper;
        public Window? HoverWindow { get; set; }
        public Window? ShowerWindow {  get; set; }
        public WindowsManager(ILogger<WindowsManager> logger, ScreenBrightnessHelper screenBrightnessHelper)
        {
            Logger = logger;
            this.screenBrightnessHelper = screenBrightnessHelper;
            Logger.LogTrace("WindowsManager created.");
        }
        internal void Initialize()
        {
            if (Settings.Instance.Hover.IsEnable)
            {
                HoverWindow = new HoverFluent();
                ShowHoverWindow();
            }
            if ((Settings.Instance.Call.NotifyMethod & 0b10) != 0) ShowerWindow = new FluentShower();
            Logger?.LogInformation("WindowsManager initialized.");
        }
        internal async Task ShowCallWindowAsync(string text, float duration, CancellationToken token)
        {
            Logger?.LogInformation("Showing call window for {duration} seconds with text: {text}", duration, text);
            var icon = new FluentIcon
            {
                Glyph = "\uECF8",
                FontSize = 60,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            var nameText = new TextBlock
            {
                Text = text,
                FontSize = 60,
                FontWeight = FontWeight.Bold,
                FontStretch = FontStretch.Expanded,
                Margin = new Thickness(15, 0, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            var showPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(25, 0),
                Children =
                {
                    icon,
                    nameText
                }
            };
            var showerWindow = new FluentShower
            {
                Content = showPanel,
                WindowStartupLocation = WindowStartupLocation.Manual,
                SizeToContent = SizeToContent.Manual,
            };
            ShowerWindow = showerWindow;

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

            var foreground = Brushes.Black;
            if (captureRect is PixelRect rect
                && screenBrightnessHelper.TryGetAverageRelativeLuminance(rect, out var luminance))
            {
                foreground = ScreenBrightnessHelper.GetRecommendedForeground(luminance) == Colors.White
                    ? Brushes.White
                    : Brushes.Black;
            }

            icon.Foreground = foreground;
            nameText.Foreground = foreground;
            showerWindow.Show();
            try
            {
                await Task.Delay((int)(duration * 1000), token);
            }
            catch { }
            showerWindow.Close();
            Logger?.LogInformation("Call window shown for {duration} seconds with text: {text}", duration, text);
        }
        internal void ShowCallWindow(string text, float duration, CancellationToken token) => _ = ShowCallWindowAsync(text, duration, token);
        internal void ShowHoverWindow()
        {
            HoverWindow ??= new HoverFluent();
            HoverWindow.Show();
            Logger?.LogInformation("Hover window shown.");
        }
        internal void HideHoverWindow()
        {
            HoverWindow?.Hide();
            Logger?.LogInformation("Hover window hidden.");
        }
        internal void CloseHoverWindow()
        {
            HoverWindow?.Close();
            HoverWindow = null;
            Logger?.LogInformation("Hover window closed.");
        }
    }
}
