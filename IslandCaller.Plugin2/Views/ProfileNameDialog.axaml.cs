using Avalonia.Controls;
using ClassIsland.Core.Controls;

namespace IslandCaller.Views;

public partial class ProfileNameDialog : Window
{
    public ProfileNameDialog(string? initialName = null)
    {
        InitializeComponent();
        NameInput.Text = initialName ?? string.Empty;
    }

    private async void ConfirmButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = NameInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            await CommonTaskDialogs.ShowDialog("名称不能为空", "请输入名单名称后再确认。", this);
            return;
        }

        Close(name);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close((string?)null);
    }
}
