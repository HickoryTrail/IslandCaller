using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using IslandCaller.Models;
using IslandCaller.Plugin2;
using IslandCaller.Plugin2.Helpers;
using IslandCaller.Services;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace IslandCaller.ViewModels;

public class SettingPageViewModel : ReactiveObject
{
    // RemoteCI 管控
    /// <summary>设置页是否被 RemoteCI 锁定；锁定后除说明信息外的所有选项只读。</summary>
    public bool IsSettingsLocked => RemotePolicy.IsSettingsLocked;

    public bool IsSettingsEditable => !IsSettingsLocked;

    /// <summary>RemoteCI 公平模式是否生效；生效时手动权重固定为 1 且不可编辑。</summary>
    public bool IsFairModeActive => RemotePolicy.IsFairModeActive;

    // 基本设置
    public bool IsBreakDisable
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool Interruptable
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // 悬浮窗设置
    public bool IsHoverEnable
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double HoverScalingFactor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int HoverLayout
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 0, 2));
    }

    public int HoverTheme
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 0, 1));
    }

    // 点名设置
    public int NotifyMethod
    {
        get => field;
        set
        {
            int notifyMethod = value & 0b11;
            if (field == notifyMethod)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref field, notifyMethod);
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

    public int ShowerTheme
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 0, 1));
    }

    public float BaseTime
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 2.0f;

    public float AdditionalTime
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1.0f;

    // TTS 设置
    public TtsProvider Provider
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<TtsProvider> TtsProviders { get; } = Enum.GetValues<TtsProvider>();

    public string BeforeText
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string AfterText
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string ExampleText
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "{学生姓名}";

    // 档案设置
    public ObservableCollection<ProfileItemViewModel> ProfileItems
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = new();

    public ProfileItemViewModel? SelectedProfile
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
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

    public bool IsPreferProfile
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private IReadOnlyList<SubjectItemViewModel> _subjectItems = [];
    public ObservableCollection<ProfilePreferenceItemViewModel> ProfilePreferenceItems
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = new();

    public ProfileService ProfileService { get; }
    private ProfileRuntimeService ProfileRuntimeService { get; }
    private IProfileService? ClassIslandProfileService { get; }
    private RemotePolicyService RemotePolicy { get; }
    private RemoteSettingsService RemoteSettings { get; }
    private bool _isAttached;

    public SettingPageViewModel()
    {
        ProfileService = IAppHost.GetService<ProfileService>();
        ProfileRuntimeService = IAppHost.GetService<ProfileRuntimeService>();
        ClassIslandProfileService = IAppHost.TryGetService<IProfileService>();
        RemotePolicy = IAppHost.GetService<RemotePolicyService>();
        RemoteSettings = IAppHost.GetService<RemoteSettingsService>();

        LoadFromSettings();
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
            else if (args.PropertyName == nameof(HoverLayout))
            {
                Settings.Instance.Hover.HoverLayout = HoverLayout;
            }
            else if (args.PropertyName == nameof(HoverTheme))
            {
                Settings.Instance.Hover.HoverTheme = HoverTheme;
            }
            else if (args.PropertyName == nameof(NotifyMethod))
            {
                Settings.Instance.Call.NotifyMethod = NotifyMethod;
            }
            else if (args.PropertyName == nameof(ShowerTheme))
            {
                Settings.Instance.Call.ShowerTheme = ShowerTheme;
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

    /// <summary>设置页显示时开始跟踪 RemoteCI 管控策略与远程设置修改，并刷新为最新值。</summary>
    public void Attach()
    {
        if (_isAttached)
        {
            return;
        }

        _isAttached = true;
        RemotePolicy.PropertyChanged += RemotePolicyOnPropertyChanged;
        RemoteSettings.SettingsApplied += RemoteSettingsOnApplied;
        RaisePolicyPropertiesChanged();
        LoadFromSettings();
        ReloadProfiles();
    }

    /// <summary>设置页隐藏时停止跟踪，避免已关闭的页面继续持有事件订阅。</summary>
    public void Detach()
    {
        if (!_isAttached)
        {
            return;
        }

        _isAttached = false;
        RemotePolicy.PropertyChanged -= RemotePolicyOnPropertyChanged;
        RemoteSettings.SettingsApplied -= RemoteSettingsOnApplied;
    }

    private void LoadFromSettings()
    {
        IsBreakDisable = Settings.Instance.General.BreakDisable;
        Interruptable = Settings.Instance.General.Interruptable;
        IsHoverEnable = Settings.Instance.Hover.IsEnable;
        HoverScalingFactor = Settings.Instance.Hover.ScalingFactor;
        HoverLayout = Settings.Instance.Hover.HoverLayout;
        HoverTheme = Settings.Instance.Hover.HoverTheme;
        NotifyMethod = Settings.Instance.Call.NotifyMethod;
        ShowerTheme = Settings.Instance.Call.ShowerTheme;
        BaseTime = Settings.Instance.Call.BaseTime;
        AdditionalTime = Settings.Instance.Call.AdditionalTime;
        Provider = Settings.Instance.TTS.Provider;
        BeforeText = Settings.Instance.TTS.BeforeText;
        AfterText = Settings.Instance.TTS.AfterText;
        IsPreferProfile = Settings.Instance.Profile.IsPreferProfile;
        ExampleText = $"{BeforeText}{{学生姓名}}{AfterText}";
    }

    private void RemotePolicyOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RemotePolicyService.IsSettingsLocked) or nameof(RemotePolicyService.IsFairModeActive))
        {
            Dispatcher.UIThread.Post(RaisePolicyPropertiesChanged);
        }
    }

    private void RemoteSettingsOnApplied(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LoadFromSettings();
            ReloadProfiles();
        });
    }

    private void RaisePolicyPropertiesChanged()
    {
        this.RaisePropertyChanged(nameof(IsSettingsLocked));
        this.RaisePropertyChanged(nameof(IsSettingsEditable));
        this.RaisePropertyChanged(nameof(IsFairModeActive));
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

        private ProfilePreferenceItemViewModel(bool isAddAction, Guid? subjectId = null, Guid? profileId = null, Action? onRuleChanged = null)
        {
            IsAddAction = isAddAction;
            // 在绑定回调前赋值，使构造期间不会触发规则变更回调。
            if (subjectId is { } subject) SubjectId = subject;
            if (profileId is { } profile) ProfileId = profile;
            _onRuleChanged = onRuleChanged;
        }

        public bool IsAddAction { get; }
        public bool IsRule => !IsAddAction;

        public bool CanAddRule
        {
            get => field;
            internal set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public Guid SubjectId
        {
            get => field;
            set
            {
                if (field == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref field, value);
                _onRuleChanged?.Invoke();
            }
        }

        public Guid ProfileId
        {
            get => field;
            set
            {
                if (field == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref field, value);
                _onRuleChanged?.Invoke();
            }
        }

        public IReadOnlyList<SubjectItemViewModel> AvailableSubjects
        {
            get => field;
            internal set => this.RaiseAndSetIfChanged(ref field, value);
        } = [];

        public IReadOnlyList<ProfileItemViewModel> AvailableProfiles
        {
            get => field;
            internal set => this.RaiseAndSetIfChanged(ref field, value);
        } = [];

        public static ProfilePreferenceItemViewModel CreateAddAction()
        {
            return new ProfilePreferenceItemViewModel(isAddAction: true);
        }

        public static ProfilePreferenceItemViewModel CreateRule(Guid subjectId, Guid profileId, Action onRuleChanged)
        {
            return new ProfilePreferenceItemViewModel(isAddAction: false, subjectId, profileId, onRuleChanged);
        }
    }
}
