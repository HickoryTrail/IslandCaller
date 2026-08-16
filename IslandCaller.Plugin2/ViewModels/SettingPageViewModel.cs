using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Plugin2;
using IslandCaller.Plugin2.Helpers;
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
    private int _notifyMethod;
    public int NotifyMethod
    {
        get => _notifyMethod;
        set
        {
            int notifyMethod = value & 0b11;
            if (_notifyMethod == notifyMethod)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _notifyMethod, notifyMethod);
            this.RaisePropertyChanged(nameof(IsClassIslandNotificationEnabled));
            this.RaisePropertyChanged(nameof(IsIslandCallerNotificationEnabled));
        }
    }

    public bool IsClassIslandNotificationEnabled
    {
        get => (NotifyMethod & 0b01) != 0;
        set => NotifyMethod = value ? NotifyMethod | 0b01 : NotifyMethod & ~0b01;
    }

    public bool IsIslandCallerNotificationEnabled
    {
        get => (NotifyMethod & 0b10) != 0;
        set => NotifyMethod = value ? NotifyMethod | 0b10 : NotifyMethod & ~0b10;
    }

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
                ProfileRuntimeService.Reload(value.ProfileId);
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

    private bool _isPreferProfile;
    public bool IsPreferProfile
    {
        get => _isPreferProfile;
        set => this.RaiseAndSetIfChanged(ref _isPreferProfile, value);
    }

    private IReadOnlyList<SubjectItemViewModel> _subjectItems = [];
    private ObservableCollection<ProfilePreferenceItemViewModel> _profilePreferenceItems = new();
    public ObservableCollection<ProfilePreferenceItemViewModel> ProfilePreferenceItems
    {
        get => _profilePreferenceItems;
        private set => this.RaiseAndSetIfChanged(ref _profilePreferenceItems, value);
    }

    public ProfileService ProfileService { get; }
    private ProfileRuntimeService ProfileRuntimeService { get; }
    private IProfileService? ClassIslandProfileService { get; }

    public SettingPageViewModel()
    {
        ProfileService = IAppHost.GetService<ProfileService>();
        ProfileRuntimeService = IAppHost.GetService<ProfileRuntimeService>();
        ClassIslandProfileService = IAppHost.TryGetService<IProfileService>();

        IsBreakDisable = Settings.Instance.General.BreakDisable;
        Interruptable = Settings.Instance.General.Interruptable;
        IsHoverEnable = Settings.Instance.Hover.IsEnable;
        HoverScalingFactor = Settings.Instance.Hover.ScalingFactor;
        NotifyMethod = Settings.Instance.Call.NotifyMethod;
        BaseTime = Settings.Instance.Call.BaseTime;
        AdditionalTime = Settings.Instance.Call.AdditionalTime;
        Provider = Settings.Instance.TTS.Provider;
        BeforeText = Settings.Instance.TTS.BeforeText;
        AfterText = Settings.Instance.TTS.AfterText;
        IsPreferProfile = Settings.Instance.Profile.IsPreferProfile;
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
            else if (args.PropertyName == nameof(NotifyMethod))
            {
                Settings.Instance.Call.NotifyMethod = NotifyMethod;
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
            else if (args.PropertyName == nameof(IsPreferProfile))
            {
                Settings.Instance.Profile.IsPreferProfile = IsPreferProfile;
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

        ReloadSubjects();
        ReloadProfilePreferenceItems();
    }

    public void AddProfilePreferenceRule()
    {
        var assignedSubjectIds = ProfilePreferenceItems
            .Where(item => item.IsRule)
            .Select(item => item.SubjectId)
            .ToHashSet();
        var subject = _subjectItems.FirstOrDefault(item => !assignedSubjectIds.Contains(item.SubjectId));
        if (subject is null || Settings.Instance.Profile.DefaultProfile == Guid.Empty)
        {
            return;
        }

        ProfilePreferenceItems.Add(ProfilePreferenceItemViewModel.CreateRule(subject.SubjectId,
            Settings.Instance.Profile.DefaultProfile, SynchronizeProfilePreferenceRules));
        SynchronizeProfilePreferenceRules();
    }

    public void RemoveProfilePreferenceRule(ProfilePreferenceItemViewModel item)
    {
        if (!item.IsRule)
        {
            return;
        }

        ProfilePreferenceItems.Remove(item);
        SynchronizeProfilePreferenceRules();
    }

    public int RemoveProfilePreferenceRulesForProfile(Guid profileId)
    {
        var rules = ProfilePreferenceItems
            .Where(item => item.IsRule && item.ProfileId == profileId)
            .ToList();
        foreach (var rule in rules)
        {
            ProfilePreferenceItems.Remove(rule);
        }

        if (rules.Count > 0)
        {
            SynchronizeProfilePreferenceRules();
        }

        return rules.Count;
    }

    public int GetProfilePreferenceRuleCount(Guid profileId)
    {
        return ProfilePreferenceItems.Count(item => item.IsRule && item.ProfileId == profileId);
    }

    private void ReloadSubjects()
    {
        object? classIslandProfile = ClassIslandProfileService?.Profile;
        _subjectItems = ClassIslandSubjectHelper.GetSubjects(classIslandProfile)
            .OrderBy(subject => subject.Name)
            .Select(subject => new SubjectItemViewModel(subject.SubjectId, subject.Name))
            .ToList();
    }

    private void ReloadProfilePreferenceItems()
    {
        var items = new ObservableCollection<ProfilePreferenceItemViewModel>
        {
            ProfilePreferenceItemViewModel.CreateAddAction()
        };
        foreach (var preference in Settings.Instance.Profile.ProfilePrefer.OrderBy(item => GetSubjectName(item.Key)))
        {
            items.Add(ProfilePreferenceItemViewModel.CreateRule(preference.Key, preference.Value,
                SynchronizeProfilePreferenceRules));
        }

        ProfilePreferenceItems = items;
        RefreshProfilePreferenceRuleOptions();
    }

    private void SynchronizeProfilePreferenceRules()
    {
        var preferences = new Dictionary<Guid, Guid>();
        foreach (var rule in ProfilePreferenceItems.Where(item => item.IsRule))
        {
            if (rule.SubjectId != Guid.Empty && rule.ProfileId != Guid.Empty)
            {
                preferences.TryAdd(rule.SubjectId, rule.ProfileId);
            }
        }

        Settings.Instance.Profile.ProfilePrefer = preferences;
        RefreshProfilePreferenceRuleOptions();
    }

    private void RefreshProfilePreferenceRuleOptions()
    {
        var rules = ProfilePreferenceItems.Where(item => item.IsRule).ToList();
        var assignedSubjectIds = rules.Select(item => item.SubjectId).ToHashSet();
        foreach (var rule in rules)
        {
            var subjects = _subjectItems.ToList();
            if (subjects.All(subject => subject.SubjectId != rule.SubjectId))
            {
                subjects.Add(new SubjectItemViewModel(rule.SubjectId, "未知科目"));
            }

            rule.AvailableSubjects = subjects
                .Where(subject => subject.SubjectId == rule.SubjectId || !assignedSubjectIds.Contains(subject.SubjectId))
                .OrderBy(subject => subject.Name)
                .ToList();
            rule.AvailableProfiles = ProfileItems.ToList();
        }

        var addAction = ProfilePreferenceItems.FirstOrDefault(item => item.IsAddAction);
        if (addAction is not null)
        {
            addAction.CanAddRule = ProfileItems.Count > 0 &&
                                   _subjectItems.Any(subject => !assignedSubjectIds.Contains(subject.SubjectId));
        }
    }

    private string GetSubjectName(Guid subjectId)
    {
        return _subjectItems.FirstOrDefault(subject => subject.SubjectId == subjectId)?.Name ?? "未知科目";
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

    public sealed class SubjectItemViewModel
    {
        public Guid SubjectId { get; }
        public string Name { get; }

        public SubjectItemViewModel(Guid subjectId, string name)
        {
            SubjectId = subjectId;
            Name = name;
        }
    }

    public sealed class ProfilePreferenceItemViewModel : ReactiveObject
    {
        private readonly Action? _onRuleChanged;
        private Guid _subjectId;
        private Guid _profileId;
        private bool _canAddRule;
        private IReadOnlyList<SubjectItemViewModel> _availableSubjects = [];
        private IReadOnlyList<ProfileItemViewModel> _availableProfiles = [];

        private ProfilePreferenceItemViewModel(bool isAddAction, Action? onRuleChanged = null)
        {
            IsAddAction = isAddAction;
            _onRuleChanged = onRuleChanged;
        }

        public bool IsAddAction { get; }
        public bool IsRule => !IsAddAction;

        public bool CanAddRule
        {
            get => _canAddRule;
            internal set => this.RaiseAndSetIfChanged(ref _canAddRule, value);
        }

        public Guid SubjectId
        {
            get => _subjectId;
            set
            {
                if (_subjectId == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _subjectId, value);
                _onRuleChanged?.Invoke();
            }
        }

        public Guid ProfileId
        {
            get => _profileId;
            set
            {
                if (_profileId == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _profileId, value);
                _onRuleChanged?.Invoke();
            }
        }

        public IReadOnlyList<SubjectItemViewModel> AvailableSubjects
        {
            get => _availableSubjects;
            internal set => this.RaiseAndSetIfChanged(ref _availableSubjects, value);
        }

        public IReadOnlyList<ProfileItemViewModel> AvailableProfiles
        {
            get => _availableProfiles;
            internal set => this.RaiseAndSetIfChanged(ref _availableProfiles, value);
        }

        public static ProfilePreferenceItemViewModel CreateAddAction()
        {
            return new ProfilePreferenceItemViewModel(isAddAction: true);
        }

        public static ProfilePreferenceItemViewModel CreateRule(Guid subjectId, Guid profileId, Action onRuleChanged)
        {
            return new ProfilePreferenceItemViewModel(isAddAction: false, onRuleChanged)
            {
                _subjectId = subjectId,
                _profileId = profileId
            };
        }
    }
}
