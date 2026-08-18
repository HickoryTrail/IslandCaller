using Avalonia.Controls;
using Avalonia.Layout;
using ClassIsland.Core.Abstractions.Controls;
using IslandCaller.Actions;

namespace IslandCaller.Controls;

/// <summary>
/// 切换档案行动的设置控件。
/// </summary>
public sealed class SwitchProfileActionSettingsControl : ActionSettingsControlBase<SwitchProfileActionSettings>
{
    private readonly ComboBox profileComboBox;
    private bool isRefreshing;

    public SwitchProfileActionSettingsControl()
    {
        profileComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "请选择档案"
        };
        profileComboBox.SelectionChanged += ProfileComboBoxOnSelectionChanged;

        Content = new StackPanel
        {
            Spacing = 10,
            Margin = new(10),
            Children =
            {
                new TextBlock { Text = "选择要加载的档案：" },
                profileComboBox
            }
        };
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        var profiles = IslandCaller.Models.Settings.Instance.Profile.ProfileList
            .OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(pair => new ProfileOption(pair.Key, pair.Value))
            .ToList();

        isRefreshing = true;
        profileComboBox.ItemsSource = profiles;
        profileComboBox.SelectedItem = profiles.FirstOrDefault(profile => profile.Id == Settings.ProfileId);
        isRefreshing = false;
    }

    private void ProfileComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isRefreshing && profileComboBox.SelectedItem is ProfileOption profile)
        {
            Settings.ProfileId = profile.Id;
        }
    }

    private sealed record ProfileOption(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }
}
