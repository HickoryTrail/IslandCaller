namespace IslandCaller.Extensions;

/// <summary>RemoteCI 插件设置页的字段类型，名称与 RemoteCI 的 ExtensionParameterType 一一对应。</summary>
internal enum RemoteSettingFieldType
{
    Text,
    Number,
    Switch,
    Select
}

/// <summary>字段联动条件，对应 RemoteCI 的 ExtensionFieldCondition：引用字段的当前值属于 Values 之一即满足。</summary>
internal sealed record RemoteSettingCondition(string Key, IReadOnlyList<string> Values, bool Not = false);

/// <summary>
/// RemoteCI 插件设置页的一个字段声明，属性与 RemoteCI 的 ExtensionParameter 同名同义。
/// RemoteCiBridge 在运行时按属性名映射到 RemoteCI 的类型，IslandCaller 因此无需在编译期引用 RemoteCI。
/// </summary>
internal sealed record RemoteSettingField(string Key, string Label, RemoteSettingFieldType Type)
{
    public bool Required { get; init; }
    public string? Description { get; init; }
    public string? Placeholder { get; init; }
    public bool Multiline { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public IReadOnlyList<string>? OptionLabels { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Step { get; init; }
    public bool Slider { get; init; }
    public string? Section { get; init; }
    public string? Card { get; init; }
    public string? CardDescription { get; init; }
    public bool CardToggle { get; init; }
    public IReadOnlyList<RemoteSettingCondition>? EnabledWhen { get; init; }
}

/// <summary>RemoteCI 回执的最小表示，由 RemoteCiBridge 转换为 RemoteCI 的 CommandResult。</summary>
internal readonly record struct RemoteCommandOutcome(bool Success, string Code, string Message)
{
    /// <summary>失败码取自 RemoteCI.Shared 的 CommandResultCodes。</summary>
    public const string OkCode = "OK";
    public const string InvalidRequestCode = "INVALID_REQUEST";
    public const string BusyCode = "BUSY";
    public const string SaveFailedCode = "SAVE_FAILED";

    public static RemoteCommandOutcome Ok(string message) => new(true, OkCode, message);

    public static RemoteCommandOutcome Failure(string code, string message) => new(false, code, message);
}
