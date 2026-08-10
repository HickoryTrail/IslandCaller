using Avalonia.Controls;
using Avalonia.Media;
using ClassIsland.Core.Controls;
using IslandCaller.Models;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace IslandCaller.Plugin2.Services
{
    internal class WindowsManager
    {
        private ILogger<WindowsManager>? Logger { get; set; }
        public Window? HoverWindow { get; set; }
        public Window? ShowerWindow {  get; set; }
        public WindowsManager(ILogger<WindowsManager> logger)
        {
            Logger = logger;
            Logger.LogTrace("WindowsManager created.");
        }
        internal void Initialize()
        {
            if (Settings.Instance.Hover.IsEnable)
            {
                HoverWindow = new HoverFluent();
                ShowHoverWindow();
            }
            if ((Settings.Instance.Call.NotifyMethod & 0b10) == 1) ShowerWindow = new FluentShower();
            Logger?.LogInformation("WindowsManager initialized.");
        }
        internal async Task ShowCallWindowAsync(string text, float duration, CancellationToken token)
        {
            Logger?.LogInformation("Showing call window for {duration} seconds with text: {text}", duration, text);
            var ShowPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(25, 0),
                Children =
                {
                    new FluentIcon
                    {
                        Glyph = "\uECF8",
                        FontSize = 60,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = text,
                        FontSize = 60,
                        FontWeight = FontWeight.Bold,
                        FontStretch = FontStretch.Expanded,
                        Margin = new Avalonia.Thickness(15, 0, 0, 0),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    }
                }
            };
            ShowerWindow = new FluentShower
            {
                Content = ShowPanel
            };
            ShowerWindow.Show();
            await Task.Delay((int)(duration * 1000), token);
            ShowerWindow.Close();
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
