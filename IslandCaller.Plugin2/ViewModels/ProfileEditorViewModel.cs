using Avalonia.Threading;
using ClassIsland.Shared;
using CommunityToolkit.Mvvm.Input;
using IslandCaller.Models;
using IslandCaller.Services;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.ViewModels;

public class ProfileEditorViewModel : ReactiveObject
{
    public Guid ProfileId { get; }

    public string ProfileName
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<StudentModel> ProfileList
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = new();

    public ICommand RowCommand { get; }

    private readonly RemotePolicyService _policy;
    private readonly ProfileService _profileService;
    private readonly ProfileSetting _profileSetting;

    /// <summary>编辑器最近一次与磁盘同步时的名单内容与名称，用于识别外部修改。</summary>
    private List<Person> _baselineMembers;
    /// <summary>与按序号排列的 _baselineMembers 逐行对应的编辑器行，用于把外部权重修改合并回原来那一行。</summary>
    private List<Guid> _baselineRowKeys = [];
    private string _baselineName;

    /// <summary>RemoteCI 公平模式生效时手动权重固定为 1，不允许编辑。</summary>
    public bool IsFairModeActive => _policy.IsFairModeActive;

    public bool IsWeightEditable => !IsFairModeActive;

    /// <summary>设置页被 RemoteCI 锁定时编辑器只读，修改不会被保存。</summary>
    public bool IsSettingsLocked => _policy.IsSettingsLocked;

    private const double BaseGridHeight = 470;
    private const double NoticeBarHeight = 60;

    /// <summary>管控提示条占用窗口高度时相应压缩名单表格，保证底部按钮在默认窗口内完整可见。</summary>
    public double GridHeight =>
        BaseGridHeight - NoticeBarHeight * ((IsSettingsLocked ? 1 : 0) + (IsFairModeActive ? 1 : 0));

    public ProfileEditorViewModel(Guid profileId)
    {
        ProfileId = profileId;
        _policy = IAppHost.GetService<RemotePolicyService>();
        _profileService = IAppHost.GetService<ProfileService>();
        _profileSetting = Settings.Instance.Profile;
        if (!_profileSetting.ProfileList.TryGetValue(profileId, out var profileName))
        {
            throw new InvalidOperationException($"找不到档案 {profileId} 的名称配置。");
        }

        ProfileName = profileName;
        _baselineName = profileName;
        _baselineMembers = _profileService.GetMembers(profileId);
        ReplaceMembers(_baselineMembers);
        ResetBaselineRows();

        RowCommand = new RelayCommand<StudentModel>(RemoveStudent);
        _policy.PropertyChanged += PolicyOnPropertyChanged;
        _profileService.ProfileSaved += OnProfileSaved;
        _profileSetting.PropertyChanged += OnProfileSettingChanged;
    }

    /// <summary>编辑器关闭时调用，解除对管控策略与名单变化的订阅。</summary>
    public void Detach()
    {
        _policy.PropertyChanged -= PolicyOnPropertyChanged;
        _profileService.ProfileSaved -= OnProfileSaved;
        _profileSetting.PropertyChanged -= OnProfileSettingChanged;
    }

    private void PolicyOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(RemotePolicyService.IsFairModeActive) or nameof(RemotePolicyService.IsSettingsLocked)))
        {
            return;
        }

        RunOnUiThread(() =>
        {
            // 公平模式生效时丢弃缓存中的旧权重，否则之后关闭公平模式、编辑器关闭时会把旧权重写回。
            if (IsFairModeActive)
            {
                foreach (var student in ProfileList)
                {
                    student.ManualWeight = 1.0;
                }
            }

            this.RaisePropertyChanged(nameof(IsFairModeActive));
            this.RaisePropertyChanged(nameof(IsWeightEditable));
            this.RaisePropertyChanged(nameof(IsSettingsLocked));
            this.RaisePropertyChanged(nameof(GridHeight));
        });
    }

    private void OnProfileSaved(object? sender, Guid profileId)
    {
        if (profileId == ProfileId)
        {
            RunOnUiThread(SyncMembersFromDisk);
        }
    }

    private void OnProfileSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProfileSetting.ProfileList))
        {
            RunOnUiThread(SyncNameFromSettings);
        }
    }

    /// <summary>
    /// 名单被编辑器之外的途径（如 RemoteCI 下发）改写后，用磁盘内容刷新编辑器，
    /// 避免关闭编辑器时把过期的缓存写回、撤销管理员的修改。编辑器自身保存后内容与磁盘一致，只更新基准。
    /// </summary>
    private void SyncMembersFromDisk()
    {
        List<Person> latest;
        try
        {
            latest = _profileService.GetMembers(ProfileId);
        }
        catch (Exception)
        {
            return;
        }

        // 磁盘已与编辑器内容一致（如编辑器自身刚保存）：把当前各行记为新的基准。导入后保存的都是新行，
        // 不重新对应的话，之后外部只修改权重时找不到这些行，关闭编辑器会把旧权重写回。
        if (SameMembers(latest, ToMembers()))
        {
            _baselineMembers = latest;
            ResetBaselineRows();
            return;
        }

        if (SameMembers(latest, _baselineMembers))
        {
            return;
        }

        if (SameRoster(latest, _baselineMembers))
        {
            // 公平模式重置、远程修改权重只改变磁盘上的权重：只合并磁盘上权重实际变化的行，
            // 保留编辑器中尚未保存的新增、改名、性别修改以及其余行的权重修改。
            var weightsByRowKey = _baselineRowKeys
                .Zip(_baselineMembers.OrderBy(member => member.Id), latest.OrderBy(member => member.Id))
                .Where(row => row.Second.ManualWeight != row.Third.ManualWeight)
                .ToDictionary(row => row.First, row => row.Third.ManualWeight);
            _baselineMembers = latest;
            foreach (var student in ProfileList)
            {
                if (weightsByRowKey.TryGetValue(student.RowKey, out double weight))
                {
                    student.ManualWeight = weight;
                }
            }

            return;
        }

        _baselineMembers = latest;
        ReplaceMembers(latest);
        ResetBaselineRows();
    }

    /// <summary>编辑器内容刚与磁盘一致时调用，把当前各行记为磁盘名单中对应的行。</summary>
    private void ResetBaselineRows()
    {
        _baselineRowKeys = ProfileList
            .OrderBy(model => model.ID)
            .Select(model => model.RowKey)
            .ToList();
    }

    private void SyncNameFromSettings()
    {
        if (!_profileSetting.ProfileList.TryGetValue(ProfileId, out var latest) || latest == _baselineName)
        {
            return;
        }

        _baselineName = latest;
        ProfileName = latest;
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    private static bool SameMembers(IEnumerable<Person> left, IEnumerable<Person> right)
    {
        var a = left.OrderBy(m => m.Id).ToList();
        var b = right.OrderBy(m => m.Id).ToList();
        return a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First.Id == pair.Second.Id &&
            pair.First.Name == pair.Second.Name &&
            pair.First.Gender == pair.Second.Gender &&
            pair.First.ManualWeight == pair.Second.ManualWeight);
    }

    private static bool SameRoster(IEnumerable<Person> left, IEnumerable<Person> right)
    {
        var a = left.OrderBy(m => m.Id).ToList();
        var b = right.OrderBy(m => m.Id).ToList();
        return a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First.Id == pair.Second.Id &&
            pair.First.Name == pair.Second.Name &&
            pair.First.Gender == pair.Second.Gender);
    }

    public void AddStudent()
    {
        int nextId = ProfileList.Any() ? ProfileList.Max(s => s.ID) + 1 : 1;
        ProfileList.Add(new StudentModel
        {
            ID = nextId,
            Name = string.Empty,
            Gender = 0,
            ManualWeight = 1.0
        });
    }

    /// <summary>
    /// 用给定名单替换编辑器内容。每行都是新行：导入的名单尚未保存，与磁盘上的学生没有对应关系，
    /// 外部只修改权重时不应合并到这些行上。
    /// </summary>
    public void ReplaceMembers(IEnumerable<Person> members)
    {
        ProfileList = new ObservableCollection<StudentModel>(members
            .OrderBy(m => m.Id)
            .Select(ToStudentModel));
    }

    public List<Person> ToMembers()
    {
        return ProfileList.Select(s => new Person
        {
            Id = s.ID,
            Name = s.Name,
            Gender = s.Gender,
            ManualWeight = IsFairModeActive ? 1.0 : s.ManualWeight
        }).ToList();
    }

    private void RemoveStudent(StudentModel? student)
    {
        if (student is not null)
        {
            ProfileList.Remove(student);
        }
    }

    private static StudentModel ToStudentModel(Person member)
    {
        return new StudentModel
        {
            ID = member.Id,
            Name = member.Name,
            Gender = member.Gender,
            ManualWeight = member.ManualWeight
        };
    }

    public class StudentModel : ReactiveObject
    {
        /// <summary>行的稳定身份；序号可在编辑器中修改，不能用来对应磁盘上的学生。</summary>
        public Guid RowKey { get; } = Guid.NewGuid();

        public int ID
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public string Name
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = string.Empty;

        public int Gender
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public double ManualWeight
        {
            get => field;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }
    }
}
