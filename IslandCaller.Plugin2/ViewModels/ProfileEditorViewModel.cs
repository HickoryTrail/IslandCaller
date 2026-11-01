using ClassIsland.Shared;
using CommunityToolkit.Mvvm.Input;
using IslandCaller.Models;
using IslandCaller.Services;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Windows.Input;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.ViewModels;

public class ProfileEditorViewModel : ReactiveObject
{
    public Guid ProfileId { get; }

    public string ProfileName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<StudentModel> ProfileList
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ICommand RowCommand { get; }

    public ProfileEditorViewModel(Guid profileId)
    {
        ProfileId = profileId;
        if (!Settings.Instance.Profile.ProfileList.TryGetValue(profileId, out var profileName))
        {
            throw new InvalidOperationException($"找不到档案 {profileId} 的名称配置。");
        }

        var profileService = IAppHost.GetService<ProfileService>();
        ProfileName = profileName;
        ProfileList = new ObservableCollection<StudentModel>(profileService.GetMembers(profileId)
            .OrderBy(m => m.Id)
            .Select(ToStudentModel));

        RowCommand = new RelayCommand<StudentModel>(RemoveStudent);
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
            ManualWeight = s.ManualWeight
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
        public int ID
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public string Name
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = string.Empty;

        public int Gender
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public double ManualWeight
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }
    }
}
