using System.Globalization;
using static IslandCaller.Services.ProfileService;

namespace IslandCaller.Helpers;

/// <summary>
/// RemoteCI 插件设置页中名单与手动权重的文本格式：
/// 名单每行一名学生，写作“姓名”或“姓名,性别”；手动权重每行写作“姓名=权重”。
/// </summary>
internal static class RosterTextHelper
{
    internal const double MinWeight = 0.0;
    internal const double MaxWeight = 4.0;
    private const int MaxNameLength = 50;
    private const int MaxReportedErrors = 5;

    private static readonly char[] RosterSeparators = [',', '，', '\t'];
    private static readonly char[] WeightSeparators = ['=', '＝', ':', '：', ',', '，', '\t'];

    internal readonly record struct RosterEntry(string Name, int Gender);
    internal readonly record struct WeightEntry(string Name, double Weight);

    internal static string FormatRoster(IEnumerable<Person> members) =>
        string.Join('\n', members
            .OrderBy(member => member.Id)
            .Select(member => $"{member.Name},{(member.Gender == 1 ? "女" : "男")}"));

    /// <summary>通常只输出权重不为 1 的学生；重名名单会输出每一行，保留行序身份。</summary>
    internal static string FormatWeights(IEnumerable<Person> members) =>
        string.Join('\n', FormatWeightEntries(members));

    private static IEnumerable<string> FormatWeightEntries(IEnumerable<Person> members)
    {
        var ordered = members.OrderBy(member => member.Id).ToList();
        var duplicateNames = ordered
            .GroupBy(member => member.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        return ordered
            .Where(member => duplicateNames.Contains(member.Name) || !IsDefaultWeight(member.ManualWeight))
            .Select(member => $"{member.Name}={member.ManualWeight.ToString("R", CultureInfo.InvariantCulture)}");
    }

    /// <summary>手动权重按原值精确比较，避免把微小的有效修改当作默认值丢弃。</summary>
    internal static bool IsDefaultWeight(double weight) => weight == 1.0;

    /// <summary>
    /// 判断提交的文本是否与本机当前值格式化后的结果一致（忽略换行符差异与首尾空行）。
    /// WebUI 单班表单会原样提交全部字段，一致即表示管理员没有修改，不必按上传格式重新解析。
    /// </summary>
    internal static bool IsUnchanged(string? submitted, string formatted) =>
        string.Equals(NormalizeText(submitted), NormalizeText(formatted), StringComparison.Ordinal);

    /// <summary>解析名单文本；格式有误时把错误追加到 errors 并返回 null。</summary>
    internal static List<RosterEntry>? ParseRoster(
        string? text,
        List<string> errors,
        bool allowDuplicateNames = false,
        bool allowBlankNames = false)
    {
        int errorCount = errors.Count;
        var entries = new List<RosterEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var lines = SplitLines(text);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = SplitRosterLine(line);
            string name = parts[0].Trim();
            // 名单文件以英文逗号分隔字段，姓名中不能再出现英文逗号。
            if (name.Contains(','))
            {
                errors.Add($"名单第 {i + 1} 行格式错误，应为“姓名”或“姓名,性别”");
                continue;
            }

            if (!IsValidName(name, $"名单第 {i + 1} 行", errors, allowBlankNames))
            {
                continue;
            }

            int gender = 0;
            if (parts.Length == 2 && !TryParseGender(parts[1].Trim(), out gender))
            {
                errors.Add($"名单第 {i + 1} 行的性别“{parts[1].Trim()}”无法识别，请填写男或女");
                continue;
            }

            if (!names.Add(name) && !allowDuplicateNames)
            {
                errors.Add($"名单中有重名学生“{name}”，请为重名学生添加区分后缀");
                continue;
            }

            entries.Add(new RosterEntry(name, gender));
        }

        return errors.Count == errorCount ? entries : null;
    }

    /// <summary>解析手动权重文本；格式有误时把错误追加到 errors 并返回 null。</summary>
    internal static List<WeightEntry>? ParseWeights(string? text, List<string> errors, bool allowBlankNames = false)
    {
        int errorCount = errors.Count;
        var weights = new List<WeightEntry>();
        var lines = SplitLines(text);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // 按最后一个分隔符切分，权重本身不含分隔符。
            int separator = line.LastIndexOfAny(WeightSeparators);
            if (separator < 0 || (!allowBlankNames && separator == 0) || separator == line.Length - 1)
            {
                errors.Add($"手动权重第 {i + 1} 行格式错误，应为“姓名=权重”");
                continue;
            }

            string name = line[..separator].Trim();
            string weightText = line[(separator + 1)..].Trim();
            if (!IsValidName(name, $"手动权重第 {i + 1} 行", errors, allowBlankNames))
            {
                continue;
            }

            if (!double.TryParse(weightText, NumberStyles.Float, CultureInfo.InvariantCulture, out double weight) ||
                !double.IsFinite(weight) || weight < MinWeight || weight > MaxWeight)
            {
                errors.Add($"手动权重第 {i + 1} 行的权重“{weightText}”需为 {MinWeight:0} 到 {MaxWeight:0} 之间的数字");
                continue;
            }

            weights.Add(new WeightEntry(name, weight));
        }

        return errors.Count == errorCount ? weights : null;
    }

    /// <summary>把多条错误合并为一条回执，过多时只列出前几条。</summary>
    internal static string JoinErrors(IReadOnlyList<string> errors) =>
        errors.Count <= MaxReportedErrors
            ? string.Join("；", errors)
            : $"{string.Join("；", errors.Take(MaxReportedErrors))}；等共 {errors.Count} 处错误";

    /// <summary>
    /// 优先按最后一个逗号或制表符切出性别，姓名中的全角逗号等字符因此可以原样保留；
    /// 没有这些分隔符时，“张三 男”这类以空格隔开性别的写法也能识别，而“John Smith”这类含空格的姓名保持不变。
    /// </summary>
    private static string[] SplitRosterLine(string line)
    {
        int separator = line.LastIndexOfAny(RosterSeparators);
        if (separator >= 0)
        {
            return [line[..separator], line[(separator + 1)..]];
        }

        int space = line.LastIndexOfAny([' ', '\u3000']);
        if (space > 0 && line[(space + 1)..].Trim() is "男" or "女")
        {
            return [line[..space], line[(space + 1)..]];
        }

        return [line];
    }

    private static bool IsValidName(string name, string location, List<string> errors, bool allowBlank)
    {
        if (name.Length == 0)
        {
            if (allowBlank)
            {
                return true;
            }

            errors.Add($"{location}缺少姓名");
            return false;
        }

        if (name.Length > MaxNameLength)
        {
            errors.Add($"{location}的姓名过长（不能超过 {MaxNameLength} 个字符）");
            return false;
        }

        return true;
    }

    private static bool TryParseGender(string text, out int gender)
    {
        switch (text.ToLowerInvariant())
        {
            case "":
            case "男":
            case "m":
            case "male":
            case "0":
                gender = 0;
                return true;
            case "女":
            case "f":
            case "female":
            case "1":
                gender = 1;
                return true;
            default:
                gender = 0;
                return false;
        }
    }

    private static string[] SplitLines(string? text) =>
        (text ?? string.Empty).ReplaceLineEndings("\n").Split('\n');

    private static string NormalizeText(string? text) =>
        (text ?? string.Empty).ReplaceLineEndings("\n").Trim('\n');
}
