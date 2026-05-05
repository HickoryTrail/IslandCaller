using static System.Guid;
using System.ComponentModel;

namespace IslandCaller.Models
{
    public class SettingsModel
    {
        public GeneralSetting General { get; set; } = new GeneralSetting();
        public ProfileSetting Profile { get; set; } = new ProfileSetting();
        public HoverSetting Hover { get; set; } = new HoverSetting();
    }

    public class GeneralSetting : INotifyPropertyChanged
    {
        public GeneralSetting()
        {
            _version = new Version(2, 0, 1, 3);
            _breakdisable = true;
        }

        private Version _version;
        public Version Version
        {
            get => _version;
        }

        private bool _breakdisable;
        public bool BreakDisable
        {
            get => _breakdisable;
            set { if (_breakdisable != value) { _breakdisable = value; OnPropertyChanged(nameof(BreakDisable)); } }
        }

        private bool _interruptable;
        public bool Interruptable { 
            get => _interruptable;
            set { if (_interruptable != value) { _interruptable = value; OnPropertyChanged(nameof(Interruptable)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProfileSetting : INotifyPropertyChanged
    {
        public ProfileSetting()
        {
            _profilenum = 1;
            _defaultprofile = NewGuid();
            _profilelist.Add(_defaultprofile, "Default");
            _ispreferprofile = false;
        }

        private int _profilenum;
        public int ProfileNum
        {
            get => _profilenum;
            set { if (_profilenum != value) { _profilenum = value; OnPropertyChanged(nameof(ProfileNum)); } }
        }

        private Guid _defaultprofile;
        public Guid DefaultProfile
        {
            get => _defaultprofile;
            set { if (_defaultprofile != value) { _defaultprofile = value; OnPropertyChanged(nameof(DefaultProfile)); } }
        }

        private Dictionary<Guid, string> _profilelist = new Dictionary<Guid, string>();
        public Dictionary<Guid, string> ProfileList
        {
            get => _profilelist;
            set { if (_profilelist != value) { _profilelist = value; OnPropertyChanged(nameof(ProfileList)); } }
        }
        private Dictionary<Guid, string> _profileprefer = new Dictionary<Guid, string>();

        private bool _ispreferprofile;
        public bool IsPreferProfile
        {
            get => _ispreferprofile;
            set { if (_ispreferprofile != value) { _ispreferprofile = value; OnPropertyChanged(nameof(IsPreferProfile)); } }
        }
        public Dictionary<Guid, string> ProfilePrefer
        {
            get => _profileprefer;
            set { if (_profileprefer != value) { _profileprefer = value; OnPropertyChanged(nameof(ProfilePrefer)); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class HoverSetting : INotifyPropertyChanged
    {
        public HoverSetting()
        {
            _isEnable = true;
            _scalingFactor = 1.0;
        }

        private bool _isEnable;
        public bool IsEnable
        {
            get => _isEnable;
            set { if (_isEnable != value) { _isEnable = value; OnPropertyChanged(nameof(IsEnable)); } }
        }

        private double _scalingFactor;

        public double ScalingFactor
        {
            get => _scalingFactor;
            set { if (_scalingFactor != value) { _scalingFactor = value; OnPropertyChanged(nameof(ScalingFactor)); } }
        }

        public PositionSetting Position { get; set; } = new PositionSetting();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PositionSetting : INotifyPropertyChanged
    {
        public PositionSetting()
        {
            _x = 200.0;
            _y = 200.0;
        }

        private double _x;
        public double X
        {
            get => _x;
            set { if (_x != value) { _x = value; OnPropertyChanged(nameof(X)); } }
        }

        private double _y;
        public double Y
        {
            get => _y;
            set { if (_y != value) { _y = value; OnPropertyChanged(nameof(Y)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}