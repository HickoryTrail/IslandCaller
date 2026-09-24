using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace IslandCaller.Helpers;

/// <summary>悬浮窗 / 结果窗口可选图片的选项（内置或本地路径）。</summary>
public sealed class ImageOption
{
    public ImageOption(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    /// <summary>选项值：空字符串表示无（默认图标），"builtin:xxx" 表示内置图片，其余为本地路径。</summary>
    public string Key { get; }

    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}

/// <summary>内置图片资源（嵌入在 avares://IslandCaller.Plugin2/Assets/ 下的 PNG）。</summary>
public static class BuiltinImages
{
    public const string Prefix = "builtin:";

    public static readonly IReadOnlyList<ImageOption> Options =
    [
        new(string.Empty, "无（默认图标）"),
        new($"{Prefix}dice", "骰子"),
        new($"{Prefix}list", "名单"),
        new($"{Prefix}star", "星星"),
        new($"{Prefix}check", "对勾"),
        new($"{Prefix}trophy", "奖杯"),
        new($"{Prefix}shuffle", "随机"),
    ];

    /// <summary>是否指向内置图片。</summary>
    public static bool IsBuiltin(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>把图片标识（内置标识或本地路径）加载为 Bitmap，失败返回 null。</summary>
    public static Bitmap? Load(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            if (IsBuiltin(value))
            {
                var name = value[Prefix.Length..];
                var uri = new Uri($"avares://IslandCaller.Plugin2/Assets/{name}.png");
                return new Bitmap(AssetLoader.Open(uri));
            }

            return File.Exists(value) ? new Bitmap(value) : null;
        }
        catch
        {
            return null;
        }
    }
}
