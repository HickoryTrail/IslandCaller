using static System.Guid;
using System.ComponentModel;
using IslandCaller.Plugin2;

namespace IslandCaller.Models
{
    public class SettingsModel
    {
        public GeneralSetting General { get; set; } = new GeneralSetting();
        public ProfileSetting Profile { get; set; } = new ProfileSetting();
        public HoverSetting Hover { get; set; } = new HoverSetting();
        public TTSSetting TTS { get; set; } = new TTSSetting();
        public CallSettings Call { get; set; } = new CallSettings();
    }

    public class GeneralSetting : INotifyPropertyChanged
    {
        public Version Version
        {
            get;
        } = new Version(2, 0, 1, 3);

        public bool BreakDisable
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(BreakDisable)); } }
        } = true;

        public bool Interruptable
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(Interruptable)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProfileSetting : INotifyPropertyChanged
    {
        public ProfileSetting()
        {
            ProfileList.Add(DefaultProfile, "Default");
        }

        public int ProfileNum
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(ProfileNum)); } }
        } = 1;

        public Guid DefaultProfile
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(DefaultProfile)); } }
        } = NewGuid();

        public Dictionary<Guid, string> ProfileList
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(ProfileList)); } }
        } = new Dictionary<Guid, string>();

        public bool IsPreferProfile
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(IsPreferProfile)); } }
        }

        public Dictionary<Guid, Guid> ProfilePrefer
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(ProfilePrefer)); } }
        } = new Dictionary<Guid, Guid>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TTSSetting : INotifyPropertyChanged
    {
        public TtsProvider Provider
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(Provider)); } }
        } = TtsProvider.None;

        public string BeforeText
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(BeforeText)); } }
        } = string.Empty;

        public string AfterText
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(AfterText)); } }
        } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CallSettings : INotifyPropertyChanged
    {
        public int ShowerTheme
        {
            get;
            set
            {
                int showerTheme = Math.Clamp(value, 0, 1);
                if (field != showerTheme)
                {
                    field = showerTheme;
                    OnPropertyChanged(nameof(ShowerTheme));
                }
            }
        }

        public int NotifyMethod
        {
            get;
            set
            {
                int notifyMethod = value & 0b11;
                if (field != notifyMethod)
                {
                    field = notifyMethod;
                    OnPropertyChanged(nameof(NotifyMethod));
                }
            }
        } = 1;

        public float BaseTime
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(BaseTime)); } }
        } = 1.0f;

        public float AdditionalTime
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(AdditionalTime)); } }
        } = 2.0f;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class HoverSetting : INotifyPropertyChanged
    {
        public bool IsEnable
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(IsEnable)); } }
        } = true;

        public double ScalingFactor
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(ScalingFactor)); } }
        } = 1.0;

        public int HoverLayout
        {
            get;
            set
            {
                int hoverLayout = Math.Clamp(value, 0, 2);
                if (field != hoverLayout)
                {
                    field = hoverLayout;
                    OnPropertyChanged(nameof(HoverLayout));
                }
            }
        }

        public int HoverTheme
        {
            get;
            set
            {
                int hoverTheme = Math.Clamp(value, 0, 1);
                if (field != hoverTheme)
                {
                    field = hoverTheme;
                    OnPropertyChanged(nameof(HoverTheme));
                }
            }
        }

        public PositionSetting Position { get; set; } = new PositionSetting();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PositionSetting : INotifyPropertyChanged
    {
        public double X
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(X)); } }
        } = 200.0;

        public double Y
        {
            get;
            set { if (field != value) { field = value; OnPropertyChanged(nameof(Y)); } }
        } = 200.0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
