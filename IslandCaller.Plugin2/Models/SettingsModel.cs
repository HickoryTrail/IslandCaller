using static System.Guid;
using System.ComponentModel;
using IslandCaller;

namespace IslandCaller.Models
{
    public class SettingsModel
    {
        public GeneralSetting General { get; set; } = new GeneralSetting();
        public ProfileSetting Profile { get; set; } = new ProfileSetting();
        public HoverSetting Hover { get; set; } = new HoverSetting();
        public TTSSetting TTS { get; set; } = new TTSSetting();
        public CallSettings Call { get; set; } = new CallSettings();
        public SecuritySetting Security { get; set; } = new SecuritySetting();
        public AlgorithmSetting Algorithm { get; set; } = new AlgorithmSetting();
        public AppearanceSetting Appearance { get; set; } = new AppearanceSetting();
        public TopmostSetting Topmost { get; set; } = new TopmostSetting();
        public HotkeySetting Hotkey { get; set; } = new HotkeySetting();
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
        private Dictionary<Guid, Guid> _profileprefer = new Dictionary<Guid, Guid>();

        private bool _ispreferprofile;
        public bool IsPreferProfile
        {
            get => _ispreferprofile;
            set { if (_ispreferprofile != value) { _ispreferprofile = value; OnPropertyChanged(nameof(IsPreferProfile)); } }
        }
        public Dictionary<Guid, Guid> ProfilePrefer
        {
            get => _profileprefer;
            set { if (_profileprefer != value) { _profileprefer = value; OnPropertyChanged(nameof(ProfilePrefer)); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TTSSetting : INotifyPropertyChanged
    {
        public TTSSetting()
        {
            _beforeText = string.Empty;
            _afterText = string.Empty;
            _provider = TtsProvider.None;
        }

        private TtsProvider _provider;
        public TtsProvider Provider
        {
            get => _provider;
            set { if (_provider != value) { _provider = value; OnPropertyChanged(nameof(Provider)); } }
        }

        private string _beforeText;
        public string BeforeText
        {
            get => _beforeText;
            set { if (_beforeText != value) { _beforeText = value; OnPropertyChanged(nameof(BeforeText)); } }
        }

        private string _afterText;
        public string AfterText
        {
            get => _afterText;
            set { if (_afterText != value) { _afterText = value; OnPropertyChanged(nameof(AfterText)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CallSettings : INotifyPropertyChanged
    {
        private int _showerTheme;
        public int ShowerTheme
        {
            get => _showerTheme;
            set
            {
                int showerTheme = Math.Clamp(value, 0, 1);
                if (_showerTheme != showerTheme)
                {
                    _showerTheme = showerTheme;
                    OnPropertyChanged(nameof(ShowerTheme));
                }
            }
        }

        private int _notifyMethod = 1;
        public int NotifyMethod
        {
            get => _notifyMethod;
            set
            {
                int notifyMethod = value & 0b11;
                if (_notifyMethod != notifyMethod)
                {
                    _notifyMethod = notifyMethod;
                    OnPropertyChanged(nameof(NotifyMethod));
                }
            }
        }

        private float _baseTime = 1.0f;
        public float BaseTime
        {
            get => _baseTime;
            set { if (_baseTime != value) { _baseTime = value; OnPropertyChanged(nameof(BaseTime)); } }
        }

        private float _additionalTime = 2.0f;
        public float AdditionalTime
        {
            get => _additionalTime;
            set { if (_additionalTime != value) { _additionalTime = value; OnPropertyChanged(nameof(AdditionalTime)); } }
        }

        private int _maxDrawCount = 5;

        /// <summary>「自定义抽取」窗口允许的最大抽取人数（1–10）。</summary>
        public int MaxDrawCount
        {
            get => _maxDrawCount;
            set
            {
                int maxDrawCount = Math.Clamp(value, 1, 10);
                if (_maxDrawCount != maxDrawCount)
                {
                    _maxDrawCount = maxDrawCount;
                    OnPropertyChanged(nameof(MaxDrawCount));
                }
            }
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
            _hoverLayout = 0;
            _hoverTheme = 0;
            _layoutDirection = 0;
            _callButtonWidth = 88;
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

        private int _hoverLayout;
        public int HoverLayout
        {
            get => _hoverLayout;
            set
            {
                int hoverLayout = Math.Clamp(value, 0, 3);
                if (_hoverLayout != hoverLayout)
                {
                    _hoverLayout = hoverLayout;
                    OnPropertyChanged(nameof(HoverLayout));
                }
            }
        }

        private int _layoutDirection;

        /// <summary>悬浮窗布局方向：0=横版（水平排列），1=竖版（垂直排列）。</summary>
        public int LayoutDirection
        {
            get => _layoutDirection;
            set
            {
                int layoutDirection = Math.Clamp(value, 0, 1);
                if (_layoutDirection != layoutDirection)
                {
                    _layoutDirection = layoutDirection;
                    OnPropertyChanged(nameof(LayoutDirection));
                }
            }
        }

        private double _callButtonWidth;

        /// <summary>
        /// 悬浮窗主按钮（Call 按钮）宽度（像素）。0 表示自动（代码内用 double.NaN）。
        /// 仅在完整（0）与简约（3）布局下生效，紧凑（1）/迷你（2）固定为 56。
        /// </summary>
        public double CallButtonWidth
        {
            get => _callButtonWidth;
            set
            {
                double callButtonWidth = Math.Clamp(value, 0, 400);
                if (_callButtonWidth != callButtonWidth)
                {
                    _callButtonWidth = callButtonWidth;
                    OnPropertyChanged(nameof(CallButtonWidth));
                }
            }
        }

        private bool _snapToScreenEdge;

        /// <summary>拖动结束后，若悬浮窗贴近屏幕边缘则自动吸附贴边。</summary>
        public bool SnapToScreenEdge
        {
            get => _snapToScreenEdge;
            set
            {
                if (_snapToScreenEdge != value)
                {
                    _snapToScreenEdge = value;
                    OnPropertyChanged(nameof(SnapToScreenEdge));
                }
            }
        }

        private int _hoverTheme;
        public int HoverTheme
        {
            get => _hoverTheme;
            set
            {
                int hoverTheme = Math.Clamp(value, 0, 1);
                if (_hoverTheme != hoverTheme)
                {
                    _hoverTheme = hoverTheme;
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

    public class SecuritySetting : INotifyPropertyChanged
    {
        // ============ 查看密码（进入设置页时校验，通过后可查看全部配置） ============

        private string _viewPasswordHash = string.Empty;

        /// <summary>查看密码的 SHA256 哈希（空字符串表示未设置查看密码）。</summary>
        public string ViewPasswordHash
        {
            get => _viewPasswordHash;
            set { if (_viewPasswordHash != value) { _viewPasswordHash = value; OnPropertyChanged(nameof(ViewPasswordHash)); } }
        }

        private bool _isViewPasswordEnabled;
        public bool IsViewPasswordEnabled
        {
            get => _isViewPasswordEnabled;
            set { if (_isViewPasswordEnabled != value) { _isViewPasswordEnabled = value; OnPropertyChanged(nameof(IsViewPasswordEnabled)); } }
        }

        // ============ 修改密码（修改配置时校验，独立于查看密码） ============

        private string _editPasswordHash = string.Empty;

        /// <summary>修改密码的 SHA256 哈希（空字符串表示未设置修改密码）。</summary>
        public string EditPasswordHash
        {
            get => _editPasswordHash;
            set { if (_editPasswordHash != value) { _editPasswordHash = value; OnPropertyChanged(nameof(EditPasswordHash)); } }
        }

        private bool _isEditPasswordEnabled;
        public bool IsEditPasswordEnabled
        {
            get => _isEditPasswordEnabled;
            set { if (_isEditPasswordEnabled != value) { _isEditPasswordEnabled = value; OnPropertyChanged(nameof(IsEditPasswordEnabled)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AlgorithmSetting : INotifyPropertyChanged
    {
        public AlgorithmSetting()
        {
            _halfRecoveryDistance = 5.0;
            _curvePower = 6.0;
            _gamma = 0.9;
            _rMin = 0.6;
            _rMax = 1.6;
        }

        private double _halfRecoveryDistance;

        /// <summary>防重复 S 曲线的半恢复距离（越大，短时间内越不容易再次抽到同一人）。</summary>
        public double HalfRecoveryDistance
        {
            get => _halfRecoveryDistance;
            set { if (value > 0 && _halfRecoveryDistance != value) { _halfRecoveryDistance = value; OnPropertyChanged(nameof(HalfRecoveryDistance)); } }
        }

        private double _curvePower;

        /// <summary>防重复 S 曲线的陡峭程度（越大曲线越陡）。</summary>
        public double CurvePower
        {
            get => _curvePower;
            set { if (value > 0 && _curvePower != value) { _curvePower = value; OnPropertyChanged(nameof(CurvePower)); } }
        }

        private double _gamma;

        /// <summary>历史均衡补偿强度（越大，被点次数少的学生权重补偿越明显）。</summary>
        public double Gamma
        {
            get => _gamma;
            set { if (value > 0 && _gamma != value) { _gamma = value; OnPropertyChanged(nameof(Gamma)); } }
        }

        private double _rMin;

        /// <summary>历史均衡因子的最小补偿系数。</summary>
        public double RMin
        {
            get => _rMin;
            set { if (value > 0 && _rMin != value) { _rMin = value; OnPropertyChanged(nameof(RMin)); } }
        }

        private double _rMax;

        /// <summary>历史均衡因子的最大补偿系数。</summary>
        public double RMax
        {
            get => _rMax;
            set { if (value > 0 && _rMax != value) { _rMax = value; OnPropertyChanged(nameof(RMax)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AppearanceSetting : INotifyPropertyChanged
    {
        public AppearanceSetting()
        {
            _accentColor = "#0078D4";
            _resultTextColor = string.Empty;
            _hoverText = "Call";
            _hoverImagePath = string.Empty;
            _resultImagePath = string.Empty;
            _fontFamily = "HarmonyOS Sans SC";
            _resultFontSize = 60;
            _resultBackground = string.Empty;
            _cornerRadius = DefaultCornerRadius;
        }

        /// <summary>界面圆角默认值（像素），与旧版按钮/玻璃容器半径接近，保证升级后观感不突变。</summary>
        public const double DefaultCornerRadius = 28;

        private string _accentColor;

        /// <summary>强调色（#RRGGBB），用于悬浮窗主按钮背景与结果图标。</summary>
        public string AccentColor
        {
            get => _accentColor;
            set { if (_accentColor != value) { _accentColor = value; OnPropertyChanged(nameof(AccentColor)); } }
        }

        private string _resultTextColor;

        /// <summary>结果文字颜色（#RRGGBB），留空表示自动按背景亮度选黑/白。</summary>
        public string ResultTextColor
        {
            get => _resultTextColor;
            set { if (_resultTextColor != value) { _resultTextColor = value; OnPropertyChanged(nameof(ResultTextColor)); } }
        }

        private string _hoverText;

        /// <summary>悬浮窗主按钮上显示的文字（默认 Call，可自定义替换）。</summary>
        public string HoverText
        {
            get => _hoverText;
            set { if (_hoverText != value) { _hoverText = value; OnPropertyChanged(nameof(HoverText)); } }
        }

        private string _hoverImagePath;

        /// <summary>抽选时悬浮窗显示的图片路径（留空则显示图标与文字）。</summary>
        public string HoverImagePath
        {
            get => _hoverImagePath;
            set { if (_hoverImagePath != value) { _hoverImagePath = value; OnPropertyChanged(nameof(HoverImagePath)); } }
        }

        private string _resultImagePath;

        /// <summary>抽选结果展示时，显示在名字前面的图片路径（留空则显示图标）。</summary>
        public string ResultImagePath
        {
            get => _resultImagePath;
            set { if (_resultImagePath != value) { _resultImagePath = value; OnPropertyChanged(nameof(ResultImagePath)); } }
        }

        private string _fontFamily;

        /// <summary>全局字体家族（悬浮窗与结果窗口）。</summary>
        public string FontFamily
        {
            get => _fontFamily;
            set { if (_fontFamily != value) { _fontFamily = value; OnPropertyChanged(nameof(FontFamily)); } }
        }

        private double _resultFontSize;

        /// <summary>结果窗口字号。</summary>
        public double ResultFontSize
        {
            get => _resultFontSize;
            set { if (value > 0 && _resultFontSize != value) { _resultFontSize = value; OnPropertyChanged(nameof(ResultFontSize)); } }
        }

        private string _resultBackground;

        /// <summary>结果窗口背景色（#AARRGGBB 或 #RRGGBB），留空表示透明。</summary>
        public string ResultBackground
        {
            get => _resultBackground;
            set { if (_resultBackground != value) { _resultBackground = value; OnPropertyChanged(nameof(ResultBackground)); } }
        }

        private double _cornerRadius;

        /// <summary>
        /// 界面圆角半径（像素，0 表示直角）。作用于悬浮窗按钮、液态玻璃容器与结果窗口。
        /// 实际生效值会被各元素自身高度限制（不超过元素半高），因此数值过大不会把圆角撑破。
        /// </summary>
        public double CornerRadius
        {
            get => _cornerRadius;
            set
            {
                double cornerRadius = value < 0 ? 0 : value;
                if (_cornerRadius != cornerRadius)
                {
                    _cornerRadius = cornerRadius;
                    OnPropertyChanged(nameof(CornerRadius));
                }
            }
        }

        private bool _keepHoverColor;

        /// <summary>
        /// 是否让悬浮窗「Call」按钮在光标悬停/按下时保持原本背景色。
        /// 关闭时沿用 ClassIsland / FluentAvalonia 的 :pointerover、:pressed 高亮效果。
        /// </summary>
        public bool KeepHoverColor
        {
            get => _keepHoverColor;
            set { if (_keepHoverColor != value) { _keepHoverColor = value; OnPropertyChanged(nameof(KeepHoverColor)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>置顶增强设置（把悬浮窗 / 结果窗口的置顶优先级提升到用户态最高级）。</summary>
    public class TopmostSetting : INotifyPropertyChanged
    {
        public TopmostSetting()
        {
            _enabled = true;
            _intervalMs = 250;
            _enableTopmostStyle = true;
            _enableToolWindow = true;
            _enableNoActivate = true;
            _enableForegroundHook = true;
            _enableUiaDetection = true;
            _extraTitleKeywords = ["FluentShower", "LiquidShower"];
        }

        private bool _enabled;

        /// <summary>是否启用置顶增强（总开关）。</summary>
        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled != value) { _enabled = value; OnPropertyChanged(nameof(Enabled)); } }
        }

        private int _intervalMs;

        /// <summary>Z 序重推周期（毫秒），建议 100~500。</summary>
        public int IntervalMs
        {
            get => _intervalMs;
            set { if (_intervalMs != value) { _intervalMs = value; OnPropertyChanged(nameof(IntervalMs)); } }
        }

        private bool _enableTopmostStyle;

        /// <summary>强化 WS_EX_TOPMOST 扩展样式。</summary>
        public bool EnableTopmostStyle
        {
            get => _enableTopmostStyle;
            set { if (_enableTopmostStyle != value) { _enableTopmostStyle = value; OnPropertyChanged(nameof(EnableTopmostStyle)); } }
        }

        private bool _enableToolWindow;

        /// <summary>附加 WS_EX_TOOLWINDOW，从 Alt+Tab 隐藏窗口。</summary>
        public bool EnableToolWindow
        {
            get => _enableToolWindow;
            set { if (_enableToolWindow != value) { _enableToolWindow = value; OnPropertyChanged(nameof(EnableToolWindow)); } }
        }

        private bool _enableNoActivate;

        /// <summary>附加 WS_EX_NOACTIVATE，避免窗口抢焦点。</summary>
        public bool EnableNoActivate
        {
            get => _enableNoActivate;
            set { if (_enableNoActivate != value) { _enableNoActivate = value; OnPropertyChanged(nameof(EnableNoActivate)); } }
        }

        private bool _enableForegroundHook;

        /// <summary>启用全局前台窗口事件钩子。</summary>
        public bool EnableForegroundHook
        {
            get => _enableForegroundHook;
            set { if (_enableForegroundHook != value) { _enableForegroundHook = value; OnPropertyChanged(nameof(EnableForegroundHook)); } }
        }

        private bool _enableUiaDetection;

        /// <summary>UIA 增强检测（DWM cloaked 识别隐藏窗口）。</summary>
        public bool EnableUiaDetection
        {
            get => _enableUiaDetection;
            set { if (_enableUiaDetection != value) { _enableUiaDetection = value; OnPropertyChanged(nameof(EnableUiaDetection)); } }
        }

        private List<string> _extraTitleKeywords;

        /// <summary>额外匹配关键词（窗口标题包含任一关键词即视为目标窗口）。</summary>
        public List<string> ExtraTitleKeywords
        {
            get => _extraTitleKeywords;
            set { if (_extraTitleKeywords != value) { _extraTitleKeywords = value; OnPropertyChanged(nameof(ExtraTitleKeywords)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
