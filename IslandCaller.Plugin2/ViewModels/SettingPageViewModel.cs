using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Plugin2;
using IslandCaller.Services;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace IslandCaller.ViewModels;

public class SettingPageViewModel : ReactiveObject
{
    // 基本设置
    private bool _isBreakDisable;
    public bool IsBreakDisable
    {
        get => _isBreakDisable;
        set => this.RaiseAndSetIfChanged(ref _isBreakDisable, value);
    }

    private bool _interruptable;
    public bool Interruptable
    {
        get => _interruptable;
        set => this.RaiseAndSetIfChanged(ref _interruptable, value);
    }

    // 悬浮窗设置
    private bool _isHoverEnable;
    public bool IsHoverEnable
    {
        get => _isHoverEnable;
        set => this.RaiseAndSetIfChanged(ref _isHoverEnable, value);
    }

    private double _hoverScalingFactor;
    public double HoverScalingFactor
    {
        get => _hoverScalingFactor;
        set => this.RaiseAndSetIfChanged(ref _hoverScalingFactor, value);
    }

    // 点名设置
    private float _baseTime = 2.0f;
    public float BaseTime
    {
        get => _baseTime;
        set => this.RaiseAndSetIfChanged(ref _baseTime, value);
    }

    private float _additionalTime = 1.0f;
    public float AdditionalTime
    {
        get => _additionalTime;
        set => this.RaiseAndSetIfChanged(ref _additionalTime, value);
    }

    // TTS 设置
    private TtsProvider _provider;
    public TtsProvider Provider
    {
        get => _provider;
        set => this.RaiseAndSetIfChanged(ref _provider, value);
    }

    public IReadOnlyList<TtsProvider> TtsProviders { get; } = Enum.GetValues<TtsProvider>();

    private string _beforeText = string.Empty;
    public string BeforeText
    {
        get => _beforeText;
        set => this.RaiseAndSetIfChanged(ref _beforeText, value);
    }

    private string _afterText = string.Empty;
    public string AfterText
    {
        get => _afterText;
        set => this.RaiseAndSetIfChanged(ref _afterText, value);
    }

    private string _exampleText = "{学生姓名}";
    public string ExampleText
    {
        get => _exampleText;
        set => this.RaiseAndSetIfChanged(ref _exampleText, value);
    }

    // 档案设置
    private ObservableCollection<ProfileItemViewModel> _profileItems = new();
    public ObservableCollection<ProfileItemViewModel> ProfileItems
    {
        get => _profileItems;
        private set => this.RaiseAndSetIfChanged(ref _profileItems, value);
    }

    private ProfileItemViewModel? _selectedProfile;
    public ProfileItemViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfile, value);
            if (value is null || value.ProfileId == Settings.Instance.Profile.DefaultProfile)
            {
                return;
            }

            var previousProfile = Settings.Instance.Profile.DefaultProfile;
            try
            {
                ProfileService.LoadSelectedProfile(value.ProfileId);
                HistoryService.Load(value.ProfileId);
                CoreService.Initialize();
                Settings.Instance.Profile.DefaultProfile = value.ProfileId;
                ReloadProfiles();
            }
            catch
            {
                Settings.Instance.Profile.DefaultProfile = previousProfile;
                ReloadProfiles();
                throw;
            }
        }
    }

    public ProfileService ProfileService { get; }
    private HistoryService HistoryService { get; }
    private CoreService CoreService { get; }

    public SettingPageViewModel()
    {
        ProfileService = IAppHost.GetService<ProfileService>();
        HistoryService = IAppHost.GetService<HistoryService>();
        CoreService = IAppHost.GetService<CoreService>();

        IsBreakDisable = Settings.Instance.General.BreakDisable;
        Interruptable = Settings.Instance.General.Interruptable;
        IsHoverEnable = Settings.Instance.Hover.IsEnable;
        HoverScalingFactor = Settings.Instance.Hover.ScalingFactor;
        BaseTime = Settings.Instance.Call.BaseTime;
        AdditionalTime = Settings.Instance.Call.AdditionalTime;
        Provider = Settings.Instance.TTS.Provider;
        BeforeText = Settings.Instance.TTS.BeforeText;
        AfterText = Settings.Instance.TTS.AfterText;
        ExampleText = $"{BeforeText}{{学生姓名}}{AfterText}";
        ReloadProfiles();

        this.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsBreakDisable))
            {
                Settings.Instance.General.BreakDisable = IsBreakDisable;
            }
            else if (args.PropertyName == nameof(Interruptable))
            {
                Settings.Instance.General.Interruptable = Interruptable;
            }
            else if (args.PropertyName == nameof(IsHoverEnable))
            {
                Settings.Instance.Hover.IsEnable = IsHoverEnable;
            }
            else if (args.PropertyName == nameof(HoverScalingFactor))
            {
                Settings.Instance.Hover.ScalingFactor = HoverScalingFactor;
            }
            else if (args.PropertyName == nameof(BaseTime))
            {
                Settings.Instance.Call.BaseTime = BaseTime;
            }
            else if (args.PropertyName == nameof(AdditionalTime))
            {
                Settings.Instance.Call.AdditionalTime = AdditionalTime;
            }
            else if (args.PropertyName == nameof(BeforeText))
            {
                Settings.Instance.TTS.BeforeText = BeforeText;
                ExampleText = $"{BeforeText}{{学生姓名}}{AfterText}";
            }
            else if (args.PropertyName == nameof(Provider))
            {
                Settings.Instance.TTS.Provider = Provider;
            }
            else if (args.PropertyName == nameof(AfterText))
            {
                Settings.Instance.TTS.AfterText = AfterText;
                ExampleText = $"{BeforeText}{{学生姓名}}{AfterText}";
            }
        };
    }

    public void ReloadProfiles()
    {
        ProfileItems = new ObservableCollection<ProfileItemViewModel>(Settings.Instance.Profile.ProfileList
            .OrderBy(profile => profile.Value)
            .Select(profile => new ProfileItemViewModel(profile.Key, profile.Value,
                profile.Key != Settings.Instance.Profile.DefaultProfile)));
        SelectedProfile = ProfileItems.FirstOrDefault(profile => profile.ProfileId == Settings.Instance.Profile.DefaultProfile);
    }

    public sealed class ProfileItemViewModel
    {
        public Guid ProfileId { get; }
        public string Name { get; }
        public bool CanDelete { get; }

        public ProfileItemViewModel(Guid profileId, string name, bool canDelete)
        {
            ProfileId = profileId;
            Name = name;
            CanDelete = canDelete;
        }
    }
}
