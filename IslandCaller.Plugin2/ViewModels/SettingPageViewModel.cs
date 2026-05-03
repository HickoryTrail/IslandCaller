using ClassIsland.Shared;
using CommunityToolkit.Mvvm.Input;
using IslandCaller.Models;
using IslandCaller.Services;
using IslandCaller.Views;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.ViewModels
{
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

        //悬浮窗设置
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

        // 档案设置
        private Guid _currentProfile = Settings.Instance.Profile.DefaultProfile;
        public Guid CurrentProfile { get => _currentProfile; }
        public class StudentModel : ReactiveObject
        {
            private int _id;
            public int ID
            {
                get => _id;
                set => this.RaiseAndSetIfChanged(ref _id, value);
            }
            private string _name;
            public string Name
            {
                get => _name;
                set => this.RaiseAndSetIfChanged(ref _name, value);
            }
            private int _gender;
            public int Gender
            {
                get => _gender;
                set => this.RaiseAndSetIfChanged(ref _gender, value);
            }
            private double _manualWeight;
            public double ManualWeight
            {
                get => _manualWeight;
                set => this.RaiseAndSetIfChanged(ref _manualWeight, value);
            }
        }
        private readonly Dictionary<StudentModel, PropertyChangedEventHandler> _handlers = new();
        private ObservableCollection<StudentModel> _profileList;
        public ObservableCollection<StudentModel> ProfileList
        {
            get => _profileList;
            set => this.RaiseAndSetIfChanged(ref _profileList, value);
        }
        public ICommand RowCommand => new RelayCommand<StudentModel>(row =>
        {
            var firstColumnValue = row.ID;
            var item = ProfileList.FirstOrDefault(p => p.ID == firstColumnValue);
            if (item != null)
            {
                ProfileList.Remove(item);
            }
        });


        // 构造函数
        public SettingPageViewModel()
        {
            ProfileService profileService = IAppHost.GetService<ProfileService>();
            HistoryService historyService = IAppHost.GetService<HistoryService>();
            CoreService coreService = IAppHost.GetService<CoreService>();
            Plugin plugin = IAppHost.GetService<Plugin>();

            // 初始化默认值
            IsBreakDisable = Settings.Instance.General.BreakDisable;
            Interruptable = Settings.Instance.General.Interruptable;
            IsHoverEnable = Settings.Instance.Hover.IsEnable;
            HoverScalingFactor = Settings.Instance.Hover.ScalingFactor;
            var profile = profileService.GetMembers(CurrentProfile)
            .OrderBy(m => m.Id)
            .Select(m => new StudentModel
            {
                ID = m.Id,
                Name = m.Name,
                Gender = m.Gender,
                ManualWeight = m.ManualWeight
            });
            ProfileList = new ObservableCollection<StudentModel>(profile);

            this.PropertyChanged += (sender, args) =>
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
                    if (IsHoverEnable)
                    {
                        plugin.HoverWindow = new HoverFluent();
                        plugin.HoverWindow.Show();
                    }
                    else plugin.HoverWindow.Close();
                }
                else if (args.PropertyName == nameof(HoverScalingFactor))
                {
                    Settings.Instance.Hover.ScalingFactor = HoverScalingFactor;
                }
                else if (args.PropertyName == nameof(ProfileList))
                {
                    List<Person> list = ProfileList
                        .Select(s => new Person
                        {
                            Id = s.ID,
                            Name = s.Name,
                            Gender = s.Gender,
                            ManualWeight = s.ManualWeight
                        }).ToList();
                    profileService.Members = list;
                    profileService.SaveProfile(CurrentProfile, list);
                    historyService.Load(CurrentProfile);
                    coreService.InitializeCore();
                }
            };
            ProfileList.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (StudentModel student in e.NewItems)
                    {
                        PropertyChangedEventHandler handler = (_, _) =>
                        {
                            List<Person> list = ProfileList
                                .Select(s => new Person
                                {
                                    Id = s.ID,
                                    Name = s.Name,
                                    Gender = s.Gender,
                                    ManualWeight = s.ManualWeight
                                }).ToList();

                            profileService.Members = list;
                            profileService.SaveProfile(CurrentProfile, list);
                            historyService.Load(CurrentProfile);
                            coreService.InitializeCore();
                        };

                        _handlers[student] = handler;
                        student.PropertyChanged += handler;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (StudentModel student in e.OldItems)
                    {
                        if (_handlers.TryGetValue(student, out var handler))
                        {
                            student.PropertyChanged -= handler;
                            _handlers.Remove(student);
                        }
                    }
                }

            };
            foreach (var student in ProfileList)
            {
                PropertyChangedEventHandler handler = (_, _) =>
                {
                    List<Person> list = ProfileList
                        .Select(s => new Person
                        {
                            Id = s.ID,
                            Name = s.Name,
                            Gender = s.Gender,
                            ManualWeight = s.ManualWeight
                        }).ToList();

                    profileService.Members = list;
                    profileService.SaveProfile(CurrentProfile, list);
                    historyService.Load(CurrentProfile);
                    coreService.InitializeCore();
                };

                _handlers[student] = handler;
                student.PropertyChanged += handler;
            }
        }

    }
}

