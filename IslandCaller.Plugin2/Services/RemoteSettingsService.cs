using System.ComponentModel;
using System.Globalization;
using IslandCaller.Extensions;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Plugin2;
using IslandCaller.Plugin2.Helpers;
using IslandCaller.Views;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Services;

/// <summary>
/// IslandCaller 在 RemoteCI 插件设置页中的设置：字段声明、当前值读取与远程写入。
/// 字段按本机设置页的分区顺序声明，使 WebUI 设置页与本机设置页保持相同结构；
/// 另外提供公平模式、锁定设置页两项只能由 RemoteCI 修改的管控开关，以及默认名单的远程查看与替换。
/// </summary>
internal sealed class RemoteSettingsService
{
    internal const string FairModeKey = "fairMode";
    internal const string LockSettingsKey = "lockSettings";
    internal const string BreakDisableKey = "breakDisable";
    internal const string InterruptableKey = "interruptable";
    internal const string HoverEnableKey = "hoverEnable";
    internal const string HoverScaleKey = "hoverScale";
    internal const string HoverLayoutKey = "hoverLayout";
    internal const string HoverThemeKey = "hoverTheme";
    internal const string NotifyClassIslandKey = "notifyClassIsland";
    internal const string NotifyIslandCallerKey = "notifyIslandCaller";
    internal const string ShowerThemeKey = "showerTheme";
    internal const string BaseTimeKey = "baseTime";
    internal const string AdditionalTimeKey = "additionalTime";
    internal const string TtsProviderKey = "ttsProvider";
    internal const string TtsBeforeKey = "ttsBefore";
    internal const string TtsAfterKey = "ttsAfter";
    internal const string RosterNameKey = "rosterName";
    internal const string RosterKey = "roster";
    internal const string WeightsKey = "weights";

    private const string ManagementSection = "RemoteCI 管控";
    private const string GeneralSection = "基本设置";
    private const string HoverSection = "悬浮窗设置";
    private const string CallSection = "点名设置";
    private const string TtsSection = "TTS 设置";
    private const string RosterSection = "名单";

    // 步长与本机设置页滑块的刻度一致；WebUI 数字框按步长校验，取值必须落在刻度上才能整表提交。
    private const double MinHoverScale = 0.5;
    private const double MaxHoverScale = 2.0;
    private const double HoverScaleStep = 0.05;
    private const double MinBaseTime = 0.0;
    private const double MaxBaseTime = 5.0;
    private const double BaseTimeStep = 0.05;
    private const double MinAdditionalTime = -5.0;
    private const double MaxAdditionalTime = 8.0;
    private const double AdditionalTimeStep = 0.1;

    // 选项值写入 WebUI 与批量下发记录，一经发布应保持稳定；下标与本机设置中的整数取值一一对应。
    private static readonly string[] HoverLayoutOptions = ["full", "compact", "mini"];
    private static readonly string[] ThemeOptions = ["fluent", "liquidGlass"];
    private static readonly string[] TtsProviderOptions = Enum.GetNames<TtsProvider>();

    private const string LiquidGlassWarning = "LiquidGlass 仍在开发中，存在崩溃风险，严禁在教学或生产环境中使用；悬浮窗与展示窗口只有一个能使用 LiquidGlass。";

    private const string ReloadFailedMessage = "重新加载当前名单失败，点名暂不可用，请检查名单与点名历史记录文件";

    /// <summary>插件设置页字段，顺序即 WebUI 中的显示顺序。</summary>
    internal static IReadOnlyList<RemoteSettingField> Fields { get; } =
    [
        new(FairModeKey, "公平模式", RemoteSettingFieldType.Switch)
        {
            Required = true,
            Section = ManagementSection,
            Description = "开启后清除本机所有名单的手动权重（全部恢复为 1），并在本机设置与此页面锁定权重编辑；关闭后已清除的权重不会恢复。"
        },
        new(LockSettingsKey, "锁定插件设置页", RemoteSettingFieldType.Switch)
        {
            Required = true,
            Section = ManagementSection,
            Description = "开启后本机 IslandCaller 设置页的所有选项变为只读，只能通过 RemoteCI 修改。"
        },

        new(BreakDisableKey, "下课禁用", RemoteSettingFieldType.Switch)
        {
            Required = true,
            Section = GeneralSection,
            Description = "在课间禁用所有点名功能"
        },
        new(InterruptableKey, "打断点名", RemoteSettingFieldType.Switch)
        {
            Required = true,
            Section = GeneralSection,
            Description = "打断上一次点名，并触发新的点名"
        },

        new(HoverEnableKey, "启用悬浮窗", RemoteSettingFieldType.Switch)
        {
            Required = true,
            Section = HoverSection,
            Description = "是否显示点名悬浮窗"
        },
        new(HoverScaleKey, "悬浮窗缩放系数", RemoteSettingFieldType.Number)
        {
            Required = true,
            Section = HoverSection,
            Description = "调整悬浮窗大小，以适配不同的屏幕",
            Min = MinHoverScale,
            Max = MaxHoverScale,
            Step = HoverScaleStep,
            Slider = true
        },
        new(HoverLayoutKey, "悬浮窗布局", RemoteSettingFieldType.Select)
        {
            Required = true,
            Section = HoverSection,
            Description = "选择完整、紧凑或 Mini 悬浮窗布局",
            Options = HoverLayoutOptions,
            OptionLabels = ["完整", "紧凑", "Mini"]
        },
        new(HoverThemeKey, "悬浮窗主题", RemoteSettingFieldType.Select)
        {
            Required = true,
            Section = HoverSection,
            Description = $"选择悬浮窗的 Fluent 或 LiquidGlass 外观。{LiquidGlassWarning}",
            Options = ThemeOptions,
            OptionLabels = ["Fluent", "LiquidGlass (Dev)"]
        },

        new(NotifyClassIslandKey, "ClassIsland 提醒", RemoteSettingFieldType.Switch)
        {
            Required = true,
            Section = CallSection,
            Card = "点名结果展示渠道",
            CardDescription = "选择展示点名结果的提醒渠道，可多选"
        },
        new(NotifyIslandCallerKey, "IslandCaller 提醒", RemoteSettingFieldType.Switch)
        {
            Required = true,
            Section = CallSection,
            Card = "点名结果展示渠道"
        },
        new(ShowerThemeKey, "点名展示主题", RemoteSettingFieldType.Select)
        {
            Required = true,
            Section = CallSection,
            Description = $"选择 IslandCaller 展示窗口的 Fluent 或 LiquidGlass 外观。{LiquidGlassWarning}",
            Options = ThemeOptions,
            OptionLabels = ["Fluent", "LiquidGlass (Dev)"]
        },
        new(BaseTimeKey, "基础时间（秒）", RemoteSettingFieldType.Number)
        {
            Required = true,
            Section = CallSection,
            Card = "点名提醒时长",
            CardDescription = "单次点名提醒的总时长为 {人数 × 基础时间 + 附加时间}",
            Min = MinBaseTime,
            Max = MaxBaseTime,
            Step = BaseTimeStep,
            Slider = true
        },
        new(AdditionalTimeKey, "附加时间（秒）", RemoteSettingFieldType.Number)
        {
            Required = true,
            Section = CallSection,
            Card = "点名提醒时长",
            Min = MinAdditionalTime,
            Max = MaxAdditionalTime,
            Step = AdditionalTimeStep,
            Slider = true
        },

        new(TtsProviderKey, "TTS 提供方", RemoteSettingFieldType.Select)
        {
            Required = true,
            Section = TtsSection,
            Description = "选择用于播报点名结果的 TTS 提供方；选择 OmniTTS 时设备需已安装 OmniTTS 插件",
            Options = TtsProviderOptions,
            OptionLabels = ["不播报", "ClassIsland", "OmniTTS"]
        },
        new(TtsBeforeKey, "播报前文本", RemoteSettingFieldType.Text)
        {
            Section = TtsSection,
            Card = "TTS 前后文",
            CardDescription = "在 TTS 播报前后添加自定义文本"
        },
        new(TtsAfterKey, "播报后文本", RemoteSettingFieldType.Text)
        {
            Section = TtsSection,
            Card = "TTS 前后文"
        },

        new(RosterNameKey, "名单名称", RemoteSettingFieldType.Text)
        {
            Required = true,
            Section = RosterSection,
            Card = "默认名单",
            CardDescription = "远程查看、编辑或上传本机默认名单；其他名单与按科目切换规则请在本机设置中管理"
        },
        // 不设为必填：本机名单可以没有学生，必填会让整表提交在 RemoteCI 校验时就被拒绝；非空由 Apply 校验。
        new(RosterKey, "学生名单", RemoteSettingFieldType.Text)
        {
            Multiline = true,
            Section = RosterSection,
            Card = "默认名单",
            Placeholder = "张三,男",
            Description = "每行一名学生，格式为“姓名”或“姓名,性别”（性别填男或女，省略为男）。保存后按行序重新编号，姓名不变的学生保留点名历史与手动权重。"
        },
        new(WeightsKey, "手动权重", RemoteSettingFieldType.Text)
        {
            Multiline = true,
            Section = RosterSection,
            Card = "默认名单",
            Placeholder = "张三=1.5",
            Description = "每行“姓名=权重”，权重范围 0 到 4，未列出的学生权重为 1。开启公平模式后此项锁定。",
            EnabledWhen = [new RemoteSettingCondition(FairModeKey, ["true"], Not: true)]
        },
    ];

    private readonly ProfileService _profileService;
    private readonly ProfileRuntimeService _profileRuntimeService;
    private readonly RemotePolicyService _policy;
    private readonly LiquidGlassRuntime _liquidGlassRuntime;
    private readonly WindowsManager _windowsManager;
    private readonly ILogger<RemoteSettingsService> _logger;
    private SettingsModel? _trackedModel;

    public RemoteSettingsService(
        ProfileService profileService,
        ProfileRuntimeService profileRuntimeService,
        RemotePolicyService policy,
        LiquidGlassRuntime liquidGlassRuntime,
        WindowsManager windowsManager,
        ILogger<RemoteSettingsService> logger)
    {
        _profileService = profileService;
        _profileRuntimeService = profileRuntimeService;
        _policy = policy;
        _liquidGlassRuntime = liquidGlassRuntime;
        _windowsManager = windowsManager;
        _logger = logger;
    }

    /// <summary>远程设置写入完成后在 UI 线程触发，已打开的设置界面据此刷新显示。</summary>
    public event EventHandler? SettingsApplied;

    /// <summary>本机设置、名单或管控策略发生变化，需要通知 RemoteCI 重新同步当前值。</summary>
    public event EventHandler? LocalSettingsChanged;

    /// <summary>
    /// 插件设置分组注册成功后调用（UI 线程）：管控策略从此生效，
    /// 并立即执行一次公平模式，覆盖离线期间经数据包导入等途径写回的权重。
    /// </summary>
    internal void Activate()
    {
        _policy.IsRemoteManaged = true;
        if (_policy.IsFairModeActive)
        {
            // 启动时没有远程请求可回执，清除或重新加载失败只记录日志：关闭公平模式前会再次清除，
            // 之后下发名单或公平模式时也会重试加载当前名单。
            EnforceFairMode(forceReload: true);
        }

        StartTracking();
    }

    internal void Deactivate()
    {
        StopTracking();
    }

    /// <summary>读取本机当前设置值，键为字段 Key，值统一为字符串。</summary>
    internal Dictionary<string, string?> GetValues()
    {
        var settings = Settings.Instance;
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [FairModeKey] = FormatBool(_policy.FairMode),
            [LockSettingsKey] = FormatBool(_policy.LockSettingsPage),
            [BreakDisableKey] = FormatBool(settings.General.BreakDisable),
            [InterruptableKey] = FormatBool(settings.General.Interruptable),
            [HoverEnableKey] = FormatBool(settings.Hover.IsEnable),
            [HoverScaleKey] = FormatNumber(settings.Hover.ScalingFactor),
            [HoverLayoutKey] = OptionAt(HoverLayoutOptions, settings.Hover.HoverLayout),
            [HoverThemeKey] = OptionAt(ThemeOptions, settings.Hover.HoverTheme),
            [NotifyClassIslandKey] = FormatBool((settings.Call.NotifyMethod & 0b01) != 0),
            [NotifyIslandCallerKey] = FormatBool((settings.Call.NotifyMethod & 0b10) != 0),
            [ShowerThemeKey] = OptionAt(ThemeOptions, settings.Call.ShowerTheme),
            [BaseTimeKey] = FormatNumber(settings.Call.BaseTime),
            [AdditionalTimeKey] = FormatNumber(settings.Call.AdditionalTime),
            [TtsProviderKey] = settings.TTS.Provider.ToString(),
            [TtsBeforeKey] = settings.TTS.BeforeText,
            [TtsAfterKey] = settings.TTS.AfterText,
        };

        Guid defaultProfileId = settings.Profile.DefaultProfile;
        values[RosterNameKey] = settings.Profile.ProfileList.GetValueOrDefault(defaultProfileId);
        try
        {
            List<ProfileService.Person> members = _profileService.GetMembers(defaultProfileId);
            values[RosterKey] = RosterTextHelper.FormatRoster(members);
            values[WeightsKey] = _policy.IsFairModeActive ? string.Empty : RosterTextHelper.FormatWeights(members);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取默认名单失败，RemoteCI 插件设置页将不显示名单");
        }

        return values;
    }

    /// <summary>
    /// 写入 RemoteCI 下发的设置（必须在 UI 线程调用）。values 只包含本次要修改的字段：
    /// 先完整校验，全部通过后再写入；未出现的字段保持不变。
    /// </summary>
    internal RemoteCommandOutcome Apply(IReadOnlyDictionary<string, string?> values, string? requestedBy)
    {
        var errors = new List<string>();
        bool? fairMode = ReadSwitch(values, FairModeKey, errors);
        bool? lockSettings = ReadSwitch(values, LockSettingsKey, errors);
        bool? breakDisable = ReadSwitch(values, BreakDisableKey, errors);
        bool? interruptable = ReadSwitch(values, InterruptableKey, errors);
        bool? hoverEnable = ReadSwitch(values, HoverEnableKey, errors);
        double? hoverScale = ReadNumber(values, HoverScaleKey, MinHoverScale, MaxHoverScale, HoverScaleStep, errors);
        int? hoverLayout = ReadSelect(values, HoverLayoutKey, HoverLayoutOptions, errors);
        int? hoverTheme = ReadSelect(values, HoverThemeKey, ThemeOptions, errors);
        bool? notifyClassIsland = ReadSwitch(values, NotifyClassIslandKey, errors);
        bool? notifyIslandCaller = ReadSwitch(values, NotifyIslandCallerKey, errors);
        int? showerTheme = ReadSelect(values, ShowerThemeKey, ThemeOptions, errors);
        double? baseTime = ReadNumber(values, BaseTimeKey, MinBaseTime, MaxBaseTime, BaseTimeStep, errors);
        double? additionalTime = ReadNumber(values, AdditionalTimeKey, MinAdditionalTime, MaxAdditionalTime, AdditionalTimeStep, errors);
        int? ttsProvider = ReadSelect(values, TtsProviderKey, TtsProviderOptions, errors);
        string? ttsBefore = ReadText(values, TtsBeforeKey);
        string? ttsAfter = ReadText(values, TtsAfterKey);

        string? rosterName = ReadText(values, RosterNameKey)?.Trim();
        if (rosterName is { Length: 0 })
        {
            errors.Add("“名单名称”不能为空");
        }

        var settings = Settings.Instance;

        // 悬浮窗与展示窗口同时使用 LiquidGlass 会触发已知的崩溃问题，远程修改不能新造出这种组合。
        // 只拦截本次把某个窗口改为 LiquidGlass 的请求：本机已有的组合随整表原样回传时不拦截，否则其他修改也会被拒绝。
        int hoverThemeAfterApply = hoverTheme ?? settings.Hover.HoverTheme;
        // 本次请求让悬浮窗开始以 LiquidGlass 显示：改为 LiquidGlass 主题，或在 LiquidGlass 主题下启用悬浮窗。
        bool hoverStartsLiquidGlass = (hoverEnable ?? settings.Hover.IsEnable) && hoverThemeAfterApply == 1 &&
                                      (settings.Hover.HoverTheme != 1 || !settings.Hover.IsEnable);
        bool switchesToLiquidGlass =
            (hoverTheme == 1 && settings.Hover.HoverTheme != 1) ||
            (showerTheme == 1 && settings.Call.ShowerTheme != 1) ||
            hoverStartsLiquidGlass;
        if (switchesToLiquidGlass &&
            hoverThemeAfterApply == 1 &&
            (showerTheme ?? settings.Call.ShowerTheme) == 1)
        {
            errors.Add("悬浮窗与点名展示窗口不能同时使用 LiquidGlass，请将其中一个改为 Fluent");
        }

        // 悬浮窗主题一改或悬浮窗一启用就会立即创建新窗口，而正在显示的点名展示窗口要等本次点名结束才关闭；
        // 此时新建 LiquidGlass 悬浮窗会让两个 LiquidGlass 窗口同时存在，需等点名结束后再修改。
        if (errors.Count == 0 && hoverStartsLiquidGlass && _windowsManager.ShowerWindow is LiquidShower)
        {
            return RemoteCommandOutcome.Failure(RemoteCommandOutcome.BusyCode,
                "点名展示窗口正在以 LiquidGlass 显示，请在本次点名结束后再把悬浮窗改为 LiquidGlass 或启用悬浮窗");
        }

        Guid defaultProfileId = settings.Profile.DefaultProfile;
        string? rosterText = ReadText(values, RosterKey);
        string? weightsText = ReadText(values, WeightsKey);
        List<ProfileService.Person>? currentMembers = null;
        int skippedRows = 0;
        Exception? currentMembersError = null;
        if (rosterText is not null || weightsText is not null)
        {
            try
            {
                currentMembers = _profileService.GetMembers(defaultProfileId, out skippedRows);
            }
            catch (Exception ex)
            {
                currentMembersError = ex;
            }

            // WebUI 单班表单会原样提交全部字段：与 GetValues 输出一致的名单和权重视为未修改，不再按上传格式解析，
            // 使含全角逗号、首尾空格等无法按上传格式精确表达的本机名单也能随其他设置一起往返。
            if (currentMembers is not null)
            {
                if (RosterTextHelper.IsUnchanged(rosterText, RosterTextHelper.FormatRoster(currentMembers)))
                {
                    rosterText = null;
                }

                if (!_policy.IsFairModeActive &&
                    RosterTextHelper.IsUnchanged(weightsText, RosterTextHelper.FormatWeights(currentMembers)))
                {
                    weightsText = null;
                }
            }
        }

        List<RosterTextHelper.RosterEntry>? roster = rosterText is not null
            ? RosterTextHelper.ParseRoster(rosterText, errors, allowDuplicateNames: true, allowBlankNames: true)
            : null;

        // 公平模式（含本次请求开启的情况）下权重字段在 WebUI 中置灰但仍会随表单提交，此时直接忽略。
        bool fairModeAfterApply = fairMode ?? _policy.FairMode;
        bool weightsIgnored = false;
        List<RosterTextHelper.WeightEntry>? weights = null;
        if (weightsText is not null)
        {
            if (fairModeAfterApply)
            {
                weightsIgnored = !string.IsNullOrWhiteSpace(weightsText);
            }
            else
            {
                weights = RosterTextHelper.ParseWeights(weightsText, errors, allowBlankNames: true);
            }
        }

        if (errors.Count > 0)
        {
            return RemoteCommandOutcome.Failure(RemoteCommandOutcome.InvalidRequestCode, RosterTextHelper.JoinErrors(errors));
        }

        var changed = new List<string>();
        var notes = new List<string>();
        List<Guid> resetFailures = [];
        // 名单与公平模式相关的请求以当前名单的实际加载状态为准：文件已写入但重新加载失败时点名暂不可用，回执需如实说明；
        // 此前重新加载失败、运行时仍未初始化时，再次下发也会重试加载，直到成功才回执成功。
        bool touchesRoster = fairMode is not null || values.ContainsKey(RosterKey) || values.ContainsKey(WeightsKey);
        bool reloadFailed = false;
        List<ProfileService.Person>? targetMembers = null;
        if (roster is not null || weights is not null)
        {
            if (currentMembersError is not null)
            {
                if (roster is null)
                {
                    _logger.LogWarning(currentMembersError, "读取默认名单失败，无法修改手动权重");
                    return RemoteCommandOutcome.Failure(RemoteCommandOutcome.SaveFailedCode, "读取本机默认名单失败，无法修改手动权重");
                }

                // 默认名单文件损坏时仍允许直接上传新名单覆盖。
                _logger.LogWarning(currentMembersError, "读取默认名单失败，将直接使用 RemoteCI 上传的名单");
            }

            targetMembers = BuildTargetMembers(currentMembers ?? [], currentMembers is not null, roster, weights, fairModeAfterApply, errors, notes);
            if (errors.Count > 0)
            {
                return RemoteCommandOutcome.Failure(RemoteCommandOutcome.InvalidRequestCode, RosterTextHelper.JoinErrors(errors));
            }

            // 名单文件中有格式错误的行时，保存会按解析结果重写整个文件并丢失这些行：
            // 上传新名单是管理员明确的整体替换，可以覆盖；只改权重则拒绝，避免顺带删除学生。
            if (skippedRows > 0 && currentMembers is not null)
            {
                var ordered = currentMembers.OrderBy(member => member.Id).ToList();
                bool keepsRoster = ordered.Count == targetMembers.Count && ordered.Zip(targetMembers).All(pair =>
                    pair.First.Id == pair.Second.Id &&
                    pair.First.Name == pair.Second.Name &&
                    pair.First.Gender == pair.Second.Gender);
                if (!keepsRoster)
                {
                    notes.Add($"原名单文件中 {skippedRows} 行格式错误，已随新名单一并覆盖");
                }
                else if (!fairModeAfterApply &&
                         ordered.Zip(targetMembers).Any(pair => pair.First.ManualWeight != pair.Second.ManualWeight))
                {
                    return RemoteCommandOutcome.Failure(RemoteCommandOutcome.SaveFailedCode,
                        $"本机默认名单文件中有 {skippedRows} 行格式错误，为避免保存时丢失这些行，未修改手动权重。" +
                        "请在本机检查名单文件，或在“学生名单”中上传完整名单覆盖");
                }
            }
        }

        try
        {
            // 先写入管控策略：关闭公平模式与新权重同时下发时，名单必须在策略关闭后保存，权重才不会被重置。
            if (lockSettings is { } lockValue && _policy.LockSettingsPage != lockValue)
            {
                _policy.LockSettingsPage = lockValue;
                changed.Add(lockValue ? "锁定设置页" : "解除设置页锁定");
            }

            if (fairMode is { } fairValue)
            {
                // 关闭前总是重新清除一次：名单文件可能绕过 SaveProfile 被替换（如导入数据包后尚未重启），
                // 只在此前有失败时才重试会让这些权重在关闭后生效。
                if (!fairValue && _policy.IsFairModeActive)
                {
                    IReadOnlyList<Guid> disableFailures = EnforceFairMode(forceReload: false).FailedProfileIds;
                    if (disableFailures.Count > 0)
                    {
                        // 锁定开关可能已在上面写入，回执需如实列出，其余设置本次不再写入。
                        string failedNames = FormatFailedProfileNames(disableFailures, settings);
                        string applied = changed.Count == 0 ? string.Empty : $"已写入：{string.Join("、", changed)}；";
                        string reload = EnsureActiveProfileLoaded() ? string.Empty : $"；{ReloadFailedMessage}";
                        return RemoteCommandOutcome.Failure(RemoteCommandOutcome.SaveFailedCode,
                            $"{applied}关闭公平模式前清除手动权重失败：{failedNames}，未关闭公平模式。请检查名单文件后重试{reload}");
                    }
                }

                bool fairModeToggled = _policy.FairMode != fairValue;
                if (fairModeToggled)
                {
                    _policy.FairMode = fairValue;
                    changed.Add(fairValue ? "开启公平模式" : "关闭公平模式");
                }

                // 即使公平模式此前已开启，重复下发也会重新清除，使上次未能写入的名单可以重试。
                if (fairValue && _policy.IsFairModeActive)
                {
                    ManualWeightResetResult reset = EnforceFairMode(forceReload: fairModeToggled);
                    resetFailures = [.. reset.FailedProfileIds];
                    if (reset.ChangedCount > 0)
                    {
                        if (fairModeToggled)
                        {
                            notes.Add($"已清除 {reset.ChangedCount} 个名单的手动权重");
                        }
                        else
                        {
                            changed.Add($"清除 {reset.ChangedCount} 个名单的手动权重");
                        }
                    }
                }
            }

            if (targetMembers is not null)
            {
                // 重新读取：本次开启公平模式时名单文件刚被重置过，对比时应以重置后的内容为准。
                ApplyMembers(defaultProfileId, ReadMembersOrEmpty(defaultProfileId), targetMembers, changed);

                // 同一请求上传的名单已按公平模式写成权重 1 时，不应再把默认名单报告为清除失败。
                if (resetFailures.Contains(defaultProfileId) &&
                    ReadMembersOrEmpty(defaultProfileId) is { Count: > 0 } saved &&
                    saved.All(member => member.ManualWeight == 1.0))
                {
                    resetFailures.Remove(defaultProfileId);
                }
            }

            if (rosterName is not null &&
                settings.Profile.ProfileList.TryGetValue(defaultProfileId, out string? currentName) &&
                currentName != rosterName)
            {
                changed.Add("名单名称");
                settings.Profile.ProfileList = new Dictionary<Guid, string>(settings.Profile.ProfileList)
                {
                    [defaultProfileId] = rosterName
                };
            }

            if (weightsIgnored)
            {
                notes.Add("公平模式下手动权重不生效");
            }

            SetValue(breakDisable, settings.General.BreakDisable, value => settings.General.BreakDisable = value, "下课禁用", changed);
            SetValue(interruptable, settings.General.Interruptable, value => settings.General.Interruptable = value, "打断点名", changed);

            // 关闭悬浮窗先于改主题、启用悬浮窗晚于改主题：否则会按旧主题临时创建一个随即被重建的悬浮窗，
            // 点名展示窗口正以 LiquidGlass 显示时就可能同时存在两个 LiquidGlass 窗口。
            if (hoverEnable == false)
            {
                SetValue(hoverEnable, settings.Hover.IsEnable, value => settings.Hover.IsEnable = value, "启用悬浮窗", changed);
            }

            SetValue(hoverScale, settings.Hover.ScalingFactor, value => settings.Hover.ScalingFactor = value, "悬浮窗缩放系数", changed);
            SetValue(hoverLayout, settings.Hover.HoverLayout, value => settings.Hover.HoverLayout = value, "悬浮窗布局", changed);
            SetValue(ResolveTheme(hoverTheme, "悬浮窗", notes), settings.Hover.HoverTheme,
                value => settings.Hover.HoverTheme = value, "悬浮窗主题", changed);
            if (hoverEnable == true)
            {
                SetValue(hoverEnable, settings.Hover.IsEnable, value => settings.Hover.IsEnable = value, "启用悬浮窗", changed);
            }

            if (notifyClassIsland is not null || notifyIslandCaller is not null)
            {
                int notifyMethod = settings.Call.NotifyMethod;
                if (notifyClassIsland is { } classIsland)
                {
                    notifyMethod = classIsland ? notifyMethod | 0b01 : notifyMethod & ~0b01;
                }

                if (notifyIslandCaller is { } islandCaller)
                {
                    notifyMethod = islandCaller ? notifyMethod | 0b10 : notifyMethod & ~0b10;
                }

                SetValue<int>(notifyMethod, settings.Call.NotifyMethod, value => settings.Call.NotifyMethod = value, "点名结果展示渠道", changed);
            }

            SetValue(ResolveTheme(showerTheme, "点名展示窗口", notes), settings.Call.ShowerTheme,
                value => settings.Call.ShowerTheme = value, "点名展示主题", changed);
            SetValue((float?)baseTime, settings.Call.BaseTime, value => settings.Call.BaseTime = value, "基础时间", changed);
            SetValue((float?)additionalTime, settings.Call.AdditionalTime, value => settings.Call.AdditionalTime = value, "附加时间", changed);

            if (ttsProvider is { } providerIndex)
            {
                var provider = (TtsProvider)providerIndex;
                if (provider == TtsProvider.OmniTTS && !CheckDependences.CheckOmniTTS())
                {
                    notes.Add("本机未安装 OmniTTS，TTS 提供方未修改");
                }
                else
                {
                    SetValue<TtsProvider>(provider, settings.TTS.Provider, value => settings.TTS.Provider = value, "TTS 提供方", changed);
                }
            }

            SetText(ttsBefore, settings.TTS.BeforeText, value => settings.TTS.BeforeText = value, "播报前文本", changed);
            SetText(ttsAfter, settings.TTS.AfterText, value => settings.TTS.AfterText = value, "播报后文本", changed);

            reloadFailed = touchesRoster && !EnsureActiveProfileLoaded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入 RemoteCI 下发的 IslandCaller 设置失败");
            // 各项在写入前就已记录：出错的那一项可能已在本机生效但未保存，回执如实提示重启后可能恢复。
            string applied = changed.Count == 0 ? string.Empty : $"已修改：{string.Join("、", changed)}；";
            string reload = touchesRoster && !EnsureActiveProfileLoaded() ? $"；{ReloadFailedMessage}" : string.Empty;
            return RemoteCommandOutcome.Failure(RemoteCommandOutcome.SaveFailedCode,
                $"{applied}写入设置时出错，部分修改可能未能保存，重启后可能恢复原值：{ex.Message}{reload}");
        }
        finally
        {
            if (changed.Count > 0)
            {
                SettingsApplied?.Invoke(this, EventArgs.Empty);
            }
        }

        if (resetFailures.Count > 0 || reloadFailed)
        {
            var failures = new List<string>();
            if (resetFailures.Count > 0)
            {
                string failedNames = FormatFailedProfileNames(resetFailures, settings);
                _logger.LogWarning("RemoteCI 用户 {User} 开启公平模式，但 {Count} 个名单的手动权重未能清除：{Names}",
                    requestedBy ?? "未知", resetFailures.Count, failedNames);
                failures.Add($"公平模式已开启，但以下名单的手动权重未能清除：{failedNames}。请检查名单文件后重新下发公平模式以重试");
            }

            if (reloadFailed)
            {
                _logger.LogWarning("RemoteCI 用户 {User} 修改了 IslandCaller 设置，但重新加载当前名单失败", requestedBy ?? "未知");
                failures.Add(ReloadFailedMessage);
            }

            string applied = changed.Count == 0 ? string.Empty : $"已写入：{string.Join("、", changed)}；";
            return RemoteCommandOutcome.Failure(RemoteCommandOutcome.SaveFailedCode, $"{applied}{string.Join("；", failures)}");
        }

        string message = changed.Count == 0 ? "设置无变化" : $"已更新 {changed.Count} 项：{string.Join("、", changed)}";
        if (notes.Count > 0)
        {
            message += $"（{string.Join("；", notes)}）";
        }

        _logger.LogInformation("RemoteCI 用户 {User} 修改了 IslandCaller 设置：{Message}", requestedBy ?? "未知", message);
        return RemoteCommandOutcome.Ok(message);
    }

    private static string FormatFailedProfileNames(IEnumerable<Guid> profileIds, SettingsModel settings)
    {
        var ids = profileIds.ToList();
        return string.Join("、", ids.Take(5)
            .Select(id => settings.Profile.ProfileList.GetValueOrDefault(id, id.ToString()))) +
            (ids.Count > 5 ? " 等" : string.Empty);
    }

    /// <summary>
    /// 把所有名单的手动权重重置为 1，并重新加载当前名单（运行时权重在加载时按公平模式计算）。
    /// 公平模式重复下发且没有任何变化时跳过重载，避免无谓地重载历史记录。
    /// </summary>
    /// <param name="forceReload">公平模式刚刚生效（开启或随插件启动）时必须重载，使运行时立即忽略手动权重。</param>
    private ManualWeightResetResult EnforceFairMode(bool forceReload)
    {
        ManualWeightResetResult result = _profileService.ResetAllManualWeights();
        if (forceReload || result.ChangedCount > 0 || result.FailedProfileIds.Count > 0)
        {
            ReloadActiveProfile();
        }

        _logger.LogInformation("公平模式已生效，重置了 {Count} 个名单的手动权重，{Failed} 个名单失败",
            result.ChangedCount, result.FailedProfileIds.Count);
        return result;
    }

    /// <summary>
    /// 重新加载当前名单、历史记录与运行时权重。失败只记录日志：这些服务会停留在未初始化状态，
    /// 由 <see cref="EnsureActiveProfileLoaded"/> 在回执前检查并重试。
    /// </summary>
    private void ReloadActiveProfile()
    {
        Guid activeProfileId = _profileService.ActiveProfileId;
        if (activeProfileId == Guid.Empty)
        {
            return;
        }

        try
        {
            _profileRuntimeService.Reload(activeProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "重新加载名单 {ProfileGuid} 失败", activeProfileId);
        }
    }

    /// <summary>当前名单（名单、历史记录与运行时权重）未完整加载时重试加载；仍然失败时点名不可用，返回 false。</summary>
    private bool EnsureActiveProfileLoaded()
    {
        Guid activeProfileId = _profileService.ActiveProfileId;
        return activeProfileId == Guid.Empty || _profileRuntimeService.EnsureLoaded(activeProfileId);
    }

    private static List<ProfileService.Person> BuildTargetMembers(
        List<ProfileService.Person> currentMembers,
        bool currentMembersLoaded,
        List<RosterTextHelper.RosterEntry>? roster,
        List<RosterTextHelper.WeightEntry>? weights,
        bool isFairMode,
        List<string> errors,
        List<string> notes)
    {
        bool rosterProvided = roster is not null;
        // 按姓名保留原有权重：重新上传名单时，姓名不变的学生不会丢失权重。
        var currentWeights = currentMembers
            .GroupBy(member => member.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ManualWeight, StringComparer.Ordinal);

        // WebUI 单班表单会提交全部字段：名单内容未变时沿用原有编号，避免每次保存其他设置都重写名单。
        var orderedCurrent = currentMembers.OrderBy(member => member.Id).ToList();
        var duplicateNames = roster?
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList() ?? [];
        bool rosterMatchesCurrent = roster is not null && roster.Count == orderedCurrent.Count && roster.Zip(orderedCurrent).All(pair =>
                pair.First.Name == pair.Second.Name && pair.First.Gender == pair.Second.Gender);
        if (rosterMatchesCurrent)
        {
            roster = null;
        }
        else if (roster is { Count: 0 })
        {
            errors.Add(currentMembersLoaded ? "名单不能为空" : "无法确认空名单是否为本机默认名单");
            return [];
        }
        else if (roster?.Any(entry => entry.Name.Length == 0) == true)
        {
            errors.Add("名单中姓名不能为空");
            return [];
        }
        else if (duplicateNames.Count > 0)
        {
            errors.Add($"名单中有重名学生“{string.Join("、", duplicateNames.Take(5))}”，新名单不能包含重名");
            return [];
        }

        List<ProfileService.Person> members = roster is not null
            ? roster.Select((entry, index) => new ProfileService.Person
            {
                Id = index + 1,
                Name = entry.Name,
                Gender = entry.Gender,
                ManualWeight = currentWeights.GetValueOrDefault(entry.Name, 1.0)
            }).ToList()
            : orderedCurrent.Select(member => new ProfileService.Person
            {
                Id = member.Id,
                Name = member.Name,
                Gender = member.Gender,
                ManualWeight = member.ManualWeight
            }).ToList();

        if (weights is not null)
        {
            var names = members.Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
            var unknownNames = weights.Select(entry => entry.Name).Distinct(StringComparer.Ordinal)
                .Where(name => !names.Contains(name)).ToList();
            if (unknownNames.Count > 0)
            {
                // 同时上传名单时，被移出名单的学生残留的权重行直接忽略；只改权重时则视为填写错误。
                string list = string.Join("、", unknownNames.Take(5)) + (unknownNames.Count > 5 ? " 等" : string.Empty);
                if (!rosterProvided)
                {
                    errors.Add($"手动权重中的学生不在名单中：{list}");
                    return members;
                }

                notes.Add($"已忽略不在名单中的权重：{list}");
            }

            var duplicateWeightNames = weights.GroupBy(entry => entry.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
            bool preserveDuplicateRows = roster is null && orderedCurrent.Count != names.Count;
            if (duplicateWeightNames.Count > 0 && !preserveDuplicateRows)
            {
                errors.Add($"手动权重中“{string.Join("、", duplicateWeightNames.Take(5))}”重复出现；只有未修改的重名名单可以按行填写");
                return members;
            }

            if (preserveDuplicateRows)
            {
                // 重名学生按出现顺序逐行对应；与其他学生一样，未列出的行恢复为 1。
                var weightsByName = weights
                    .GroupBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Select(entry => entry.Weight).ToList(), StringComparer.Ordinal);
                var memberCounts = members
                    .GroupBy(member => member.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                var extraRows = weightsByName
                    .Where(pair => pair.Value.Count > memberCounts.GetValueOrDefault(pair.Key))
                    .Select(pair => pair.Key)
                    .ToList();
                if (extraRows.Count > 0)
                {
                    errors.Add($"手动权重中“{string.Join("、", extraRows.Take(5))}”的行数多于名单中的同名学生");
                    return members;
                }

                var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var member in members)
                {
                    int occurrence = occurrences.GetValueOrDefault(member.Name);
                    occurrences[member.Name] = occurrence + 1;
                    member.ManualWeight = weightsByName.TryGetValue(member.Name, out var rowWeights) && occurrence < rowWeights.Count
                        ? rowWeights[occurrence]
                        : 1.0;
                }
            }
            else
            {
                var weightsByName = weights.ToDictionary(entry => entry.Name, entry => entry.Weight, StringComparer.Ordinal);
                foreach (var member in members)
                {
                    member.ManualWeight = weightsByName.GetValueOrDefault(member.Name, 1.0);
                }
            }
        }

        if (isFairMode)
        {
            foreach (var member in members)
            {
                member.ManualWeight = 1.0;
            }
        }

        return members;
    }

    private List<ProfileService.Person> ReadMembersOrEmpty(Guid profileId)
    {
        try
        {
            return _profileService.GetMembers(profileId);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private void ApplyMembers(
        Guid profileId,
        List<ProfileService.Person> currentMembers,
        List<ProfileService.Person> targetMembers,
        List<string> changed)
    {
        var current = currentMembers.OrderBy(member => member.Id).ToList();
        bool membersChanged = current.Count != targetMembers.Count || current.Zip(targetMembers).Any(pair =>
            pair.First.Id != pair.Second.Id ||
            pair.First.Name != pair.Second.Name ||
            pair.First.Gender != pair.Second.Gender);
        // 权重按原值精确比较：文本格式往返不丢精度，微小但有效的修改也必须写入。
        bool weightsChanged = !membersChanged && current.Zip(targetMembers).Any(pair =>
            pair.First.ManualWeight != pair.Second.ManualWeight);
        if (!membersChanged && !weightsChanged)
        {
            return;
        }

        changed.Add(membersChanged ? "学生名单" : "手动权重");
        _profileService.SaveProfile(profileId, targetMembers);
        if (_profileService.ActiveProfileId == profileId)
        {
            ReloadActiveProfile();
        }
    }

    /// <summary>LiquidGlass 着色器不可用时改用 Fluent，避免下发后窗口创建时再被静默回退。</summary>
    private int? ResolveTheme(int? theme, string target, List<string> notes)
    {
        if (theme == 1 && !_liquidGlassRuntime.IsReady)
        {
            notes.Add($"本机 LiquidGlass 不可用，{target}已改用 Fluent");
            return 0;
        }

        return theme;
    }

    private void StartTracking()
    {
        StopTracking();
        _trackedModel = Settings.Instance;
        _trackedModel.General.PropertyChanged += OnLocalSettingChanged;
        _trackedModel.Hover.PropertyChanged += OnLocalSettingChanged;
        _trackedModel.Call.PropertyChanged += OnLocalSettingChanged;
        _trackedModel.TTS.PropertyChanged += OnLocalSettingChanged;
        _trackedModel.Profile.PropertyChanged += OnLocalSettingChanged;
        _profileService.ProfileSaved += OnProfileSaved;
        _policy.PropertyChanged += OnLocalSettingChanged;
    }

    private void StopTracking()
    {
        if (_trackedModel is { } model)
        {
            model.General.PropertyChanged -= OnLocalSettingChanged;
            model.Hover.PropertyChanged -= OnLocalSettingChanged;
            model.Call.PropertyChanged -= OnLocalSettingChanged;
            model.TTS.PropertyChanged -= OnLocalSettingChanged;
            model.Profile.PropertyChanged -= OnLocalSettingChanged;
            _trackedModel = null;
        }

        _profileService.ProfileSaved -= OnProfileSaved;
        _policy.PropertyChanged -= OnLocalSettingChanged;
    }

    private void OnLocalSettingChanged(object? sender, PropertyChangedEventArgs e) =>
        LocalSettingsChanged?.Invoke(this, EventArgs.Empty);

    private void OnProfileSaved(object? sender, Guid profileId) =>
        LocalSettingsChanged?.Invoke(this, EventArgs.Empty);

    private static void SetValue<T>(T? value, T current, Action<T> set, string label, List<string> changed)
        where T : struct
    {
        if (value is not { } newValue || EqualityComparer<T>.Default.Equals(current, newValue))
        {
            return;
        }

        // 先记录再赋值：模型赋值后才由 Settings.Save 写入注册表，写入抛出时这一项已在本机生效，回执需要列出。
        changed.Add(label);
        set(newValue);
    }

    private static void SetText(string? value, string current, Action<string> set, string label, List<string> changed)
    {
        if (value is null || string.Equals(current, value, StringComparison.Ordinal))
        {
            return;
        }

        changed.Add(label);
        set(value);
    }

    private static bool? ReadSwitch(IReadOnlyDictionary<string, string?> values, string key, List<string> errors)
    {
        if (!values.TryGetValue(key, out string? raw))
        {
            return null;
        }

        if (bool.TryParse(raw?.Trim(), out bool value))
        {
            return value;
        }

        errors.Add($"“{LabelOf(key)}”只能是开启或关闭");
        return null;
    }

    /// <summary>
    /// 读取数字字段：取值必须在范围内且落在步长刻度上（只容许浮点运算误差），
    /// 否则 GetValues 回传的值会被 WebUI 数字框判为无效，整表无法再次提交。
    /// 合法值原样返回，本机滑块产生的值因此能精确往返，不会被误报为已修改。
    /// </summary>
    private static double? ReadNumber(
        IReadOnlyDictionary<string, string?> values, string key, double min, double max, double step, List<string> errors)
    {
        if (!values.TryGetValue(key, out string? raw))
        {
            return null;
        }

        if (double.TryParse(raw?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
            double.IsFinite(value) && value >= min && value <= max)
        {
            double nearestTick = min + Math.Round((value - min) / step) * step;
            if (Math.Abs(value - nearestTick) <= step * 1e-9)
            {
                return value;
            }
        }

        errors.Add($"“{LabelOf(key)}”需为 {FormatNumber(min)} 到 {FormatNumber(max)} 之间、步长为 {FormatNumber(step)} 的数字");
        return null;
    }

    private static int? ReadSelect(
        IReadOnlyDictionary<string, string?> values, string key, IReadOnlyList<string> options, List<string> errors)
    {
        if (!values.TryGetValue(key, out string? raw))
        {
            return null;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i], raw?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        errors.Add($"“{LabelOf(key)}”不是有效选项");
        return null;
    }

    private static string? ReadText(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out string? raw) ? raw ?? string.Empty : null;

    private static string LabelOf(string key) =>
        Fields.FirstOrDefault(field => field.Key == key) is { } field
            ? field.Card is null ? field.Label : $"{field.Card} · {field.Label}"
            : key;

    private static string FormatBool(bool value) => value ? "true" : "false";

    private static string FormatNumber(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>float 设置按自身精度输出，避免扩宽为 double 后出现 0.3499999940395355 这类无法通过步长校验的值。</summary>
    private static string FormatNumber(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string OptionAt(IReadOnlyList<string> options, int index) =>
        options[Math.Clamp(index, 0, options.Count - 1)];
}
