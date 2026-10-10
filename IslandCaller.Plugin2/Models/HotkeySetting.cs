using System.ComponentModel;

namespace IslandCaller.Models
{
    /// <summary>全局快捷键修饰键（可与 Win32 MOD_* 常量互转）。</summary>
    [Flags]
    public enum HotkeyModifiers
    {
        None = 0,
        Control = 1,
        Alt = 2,
        Shift = 4,
        Windows = 8
    }

    /// <summary>全局快捷键的触发范围。</summary>
    public enum HotkeyScope
    {
        /// <summary>全局：任意前台窗口均触发（不依赖 ClassIsland 是否获得焦点）。</summary>
        Global = 0,

        /// <summary>仅 ClassIsland 前台：仅当 ClassIsland（含本插件悬浮窗）位于前台时触发。</summary>
        ClassIslandForeground = 1
    }

    /// <summary>
    /// 全局快捷键点名设置。
    /// 组合方式 = 修饰键（Ctrl/Alt/Shift/Win，至少一个）+ 主键（A–Z / 0–9 / F1–F24 / 若干特殊键）；
    /// 仅功能键（F1–F24）允许不带修饰键单独使用，避免与日常打字冲突。
    /// </summary>
    public class HotkeySetting : INotifyPropertyChanged
    {
        public HotkeySetting()
        {
            _enabled = false;
            _modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt;
            _key = "C";
            _scope = HotkeyScope.Global;
        }

        private bool _enabled;

        /// <summary>是否启用全局快捷键点名（默认关闭，避免与其它软件或用户习惯冲突）。</summary>
        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled != value) { _enabled = value; OnPropertyChanged(nameof(Enabled)); } }
        }

        private HotkeyModifiers _modifiers;

        /// <summary>修饰键组合（至少一个；功能键可例外）。</summary>
        public HotkeyModifiers Modifiers
        {
            get => _modifiers;
            set
            {
                if (_modifiers != value)
                {
                    _modifiers = value;
                    OnPropertyChanged(nameof(Modifiers));
                    OnPropertyChanged(nameof(GestureText));
                }
            }
        }

        private string _key;

        /// <summary>主键标识（A–Z / 0–9 / F1–F24 / Space / Insert 等）。</summary>
        public string Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged(nameof(Key));
                    OnPropertyChanged(nameof(GestureText));
                }
            }
        }

        private HotkeyScope _scope;

        /// <summary>触发范围。</summary>
        public HotkeyScope Scope
        {
            get => _scope;
            set
            {
                if (_scope != value)
                {
                    _scope = value;
                    OnPropertyChanged(nameof(Scope));
                }
            }
        }

        /// <summary>可读的快捷键文本，例如 "Ctrl + Alt + C"。</summary>
        public string GestureText => HotkeyText.Format(Modifiers, Key);

        /// <summary>是否为合法的组合（主键受支持，且字母/数字/特殊键至少带一个修饰键）。</summary>
        public bool IsValid => HotkeyText.Validate(Modifiers, Key, out _);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>快捷键文本格式化与合法性校验。</summary>
    public static class HotkeyText
    {
        /// <summary>把修饰键 + 主键格式化为可读文本。</summary>
        public static string Format(HotkeyModifiers modifiers, string? key)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
            parts.Add(DisplayKey(key));
            return string.Join(" + ", parts);
        }

        /// <summary>主键的展示名（空格等特殊键转为中文）。</summary>
        public static string DisplayKey(string? key) => key switch
        {
            null or "" => "未设置",
            "Space" => "空格",
            "Insert" => "Insert",
            "Delete" => "Delete",
            "Home" => "Home",
            "End" => "End",
            "PageUp" => "PageUp",
            "PageDown" => "PageDown",
            "Left" => "←",
            "Right" => "→",
            "Up" => "↑",
            "Down" => "↓",
            "OemMinus" => "-",
            "OemPlus" => "=",
            "OemComma" => ",",
            "OemPeriod" => ".",
            "OemQuestion" => "/",
            _ => key
        };

        /// <summary>是否为功能键（F1–F24，允许不带修饰键单独使用）。</summary>
        public static bool IsFunctionKey(string? key) =>
            key is { Length: >= 2 and <= 3 } && key[0] == 'F'
            && int.TryParse(key.AsSpan(1), out int index) && index is >= 1 and <= 24;

        /// <summary>校验组合是否合法，并返回不合法原因。</summary>
        public static bool Validate(HotkeyModifiers modifiers, string? key, out string error)
        {
            if (string.IsNullOrEmpty(key))
            {
                error = "请先选择主键。";
                return false;
            }

            if (modifiers == HotkeyModifiers.None && !IsFunctionKey(key))
            {
                error = "字母、数字与特殊键必须至少配合一个修饰键（Ctrl/Alt/Shift/Win）；仅功能键 F1–F24 可单独使用。";
                return false;
            }

            if (!Helpers.HotkeyKeyMap.IsSupported(key))
            {
                error = $"不支持的主键：{DisplayKey(key)}。";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
