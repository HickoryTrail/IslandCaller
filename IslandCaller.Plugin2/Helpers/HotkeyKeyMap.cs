namespace IslandCaller.Helpers;

/// <summary>
/// 快捷键主键 → Win32 虚拟键码（VK）映射。
/// 仅暴露经过挑选的主键，避免用户把整块键盘都绑成全局快捷键导致系统输入异常。
/// </summary>
internal static class HotkeyKeyMap
{
    /// <summary>下拉框可选的主键（顺序即展示顺序）。</summary>
    public static IReadOnlyList<string> SelectableKeys { get; } = BuildSelectableKeys();

    private static readonly Dictionary<string, uint> SpecialKeys = new(StringComparer.Ordinal)
    {
        ["Space"] = 0x20,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["OemMinus"] = 0xBD,
        ["OemPlus"] = 0xBB,
        ["OemComma"] = 0xBC,
        ["OemPeriod"] = 0xBE,
        ["OemQuestion"] = 0xBF
    };

    /// <summary>主键是否受支持。</summary>
    public static bool IsSupported(string? key) => TryGetVirtualKey(key, out _);

    /// <summary>把主键标识解析为 Win32 虚拟键码。</summary>
    public static bool TryGetVirtualKey(string? key, out uint virtualKey)
    {
        virtualKey = 0;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        // 字母 A–Z → 0x41–0x5A
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z')
        {
            virtualKey = key[0];
            return true;
        }

        // 数字 0–9 → 0x30–0x39
        if (key.Length == 1 && key[0] is >= '0' and <= '9')
        {
            virtualKey = key[0];
            return true;
        }

        // 功能键 F1–F24 → 0x70 起
        if (key[0] == 'F' && int.TryParse(key.AsSpan(1), out int functionIndex)
            && functionIndex is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionIndex - 1);
            return true;
        }

        return SpecialKeys.TryGetValue(key, out virtualKey);
    }

    private static List<string> BuildSelectableKeys()
    {
        var keys = new List<string>();
        for (char c = 'A'; c <= 'Z'; c++)
        {
            keys.Add(c.ToString());
        }

        for (char c = '0'; c <= '9'; c++)
        {
            keys.Add(c.ToString());
        }

        for (int i = 1; i <= 12; i++)
        {
            keys.Add($"F{i}");
        }

        keys.AddRange([
            "Space", "Insert", "Delete", "Home", "End",
            "PageUp", "PageDown", "Left", "Up", "Right", "Down",
            "OemMinus", "OemPlus", "OemComma", "OemPeriod", "OemQuestion"
        ]);

        return keys;
    }
}
