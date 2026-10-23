using System.Collections;
using System.Reflection;
using Avalonia.Threading;
using ClassIsland.Shared;
using IslandCaller.Services;
using IslandCaller.Services.IslandCallerService;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Extensions;

/// <summary>
/// RemoteCI 运行时桥：通过反射发现并注册远程扩展与插件设置页，避免 IslandCaller 静态引用 RemoteCI.Plugin.dll。
/// 这样即使 RemoteCI 未安装、版本较旧、或插件加载顺序不理想，IslandCaller 自身也能正常启动。
/// </summary>
public static class RemoteCiBridge
{
    /// <summary>远程扩展的全局唯一 Id，须保持稳定。</summary>
    internal const string ExtensionId = "IslandCaller.RandomCall";

    /// <summary>插件设置分组的全局唯一 Id，须保持稳定：服务端按它保存各班设置与待补发记录。</summary>
    internal const string GroupId = "IslandCaller";

    internal const string GroupDisplayName = "IslandCaller";

    /// <summary>
    /// 手表端展示的 Material 图标名。
    /// 注意：RemoteCI 手表端只认编译期固化的内置图标名白名单（手表端 ExtensionIcons.kt），
    /// 白名单外的名字会被退化为纯文字，因此这里的取值必须来自白名单。可选值见 RemoteCI
    /// 文档站「接入扩展 - 扩展图标名」，casino 属于「随机与刷新」分类（shuffle/casino/autorenew/cached）。
    /// 若要使用 IslandCaller 自己的图标资源，需要 RemoteCI 侧扩展协议支持图片图标后再调整。
    /// </summary>
    internal const string WatchIconName = "casino";

    private const string RemoteCiPluginAssemblyName = "RemoteCI.Plugin";
    private const string RegistryTypeName = "RemoteCI.Plugin.Extensions.IRemoteCiExtensionRegistry";
    private const string ExtensionInterfaceName = "RemoteCI.Plugin.Extensions.IRemoteCiExtension";
    private const string GroupInterfaceName = "RemoteCI.Plugin.Extensions.IRemoteCiExtensionGroup";

    /// <summary>本机设置连续变化（如拖动滑块）时合并为一次同步，避免频繁上报服务端。</summary>
    private static readonly TimeSpan NotifyDelay = TimeSpan.FromSeconds(1);

    private static readonly object SyncRoot = new();
    private static object? _registry;
    private static Type? _registryType;
    private static RemoteSettingsService? _remoteSettings;
    private static Timer? _notifyTimer;
    private static ILogger? _logger;
    private static bool _extensionRegistered;
    private static volatile bool _groupRegistered;

    /// <summary>插件设置分组是否已注册；随机点名扩展据此决定是否归入该分组。</summary>
    internal static bool IsGroupRegistered => _groupRegistered;

    /// <summary>
    /// 向 RemoteCI 注册插件设置页与“随机点名”远程扩展；RemoteCI 未加载或接口缺失时静默跳过。
    /// 旧版 RemoteCI 不支持扩展分组时只注册随机点名扩展。
    /// </summary>
    public static void Register(ILogger? logger)
    {
        lock (SyncRoot)
        {
            _logger = logger;
            var assembly = FindRemoteCiAssembly();
            if (assembly is null)
            {
                logger?.LogDebug("未检测到 RemoteCI 插件，跳过远程扩展注册");
                return;
            }

            _registryType = assembly.GetType(RegistryTypeName);
            var extensionInterfaceType = assembly.GetType(ExtensionInterfaceName);
            if (_registryType is null || extensionInterfaceType is null)
            {
                logger?.LogWarning("RemoteCI 插件版本不包含扩展接口，跳过远程扩展注册");
                return;
            }

            _registry = GetRegistryService(_registryType);
            if (_registry is null)
            {
                logger?.LogWarning("未取得 RemoteCI 扩展注册表服务，跳过远程扩展注册");
                return;
            }

            // 先注册分组，随机点名扩展的 GroupId 才能指向已存在的分组。
            RegisterSettingsGroup(assembly, logger);

            var proxy = RemoteCiExtensionProxy.Create(extensionInterfaceType, logger);
            _registryType.GetMethod("Register")!.Invoke(_registry, new object[] { proxy });
            _extensionRegistered = true;
            logger?.LogInformation("已注册 RemoteCI 远程扩展：随机点名");
        }
    }

    /// <summary>注销已注册的远程扩展与插件设置页，避免退出后残留无效入口。</summary>
    public static void Unregister(ILogger? logger)
    {
        lock (SyncRoot)
        {
            if (_remoteSettings is not null)
            {
                _remoteSettings.LocalSettingsChanged -= OnLocalSettingsChanged;
                _remoteSettings.Deactivate();
                _remoteSettings = null;
            }

            _notifyTimer?.Dispose();
            _notifyTimer = null;

            if (_registry is null || _registryType is null) return;
            if (_extensionRegistered)
            {
                TryInvokeRegistry("Unregister", ExtensionId, logger);
                _extensionRegistered = false;
            }

            if (_groupRegistered)
            {
                TryInvokeRegistry("UnregisterGroup", GroupId, logger);
                _groupRegistered = false;
            }
        }
    }

    private static void RegisterSettingsGroup(Assembly assembly, ILogger? logger)
    {
        var groupInterfaceType = assembly.GetType(GroupInterfaceName);
        var registerGroup = _registryType!.GetMethod("RegisterGroup");
        if (groupInterfaceType is null || registerGroup is null)
        {
            logger?.LogInformation("当前 RemoteCI 版本不支持插件设置页，仅注册随机点名扩展");
            return;
        }

        try
        {
            var remoteSettings = IAppHost.GetService<RemoteSettingsService>();
            var proxy = RemoteCiGroupProxy.Create(groupInterfaceType, remoteSettings, logger);
            registerGroup.Invoke(_registry, new[] { proxy });
            _groupRegistered = true;
            _remoteSettings = remoteSettings;
            _notifyTimer = new Timer(_ => NotifySettingsChanged(), null, Timeout.Infinite, Timeout.Infinite);
            remoteSettings.LocalSettingsChanged += OnLocalSettingsChanged;
            remoteSettings.Activate();
            // 注册时 RemoteCI 已读取过一次当前值；Activate 可能刚执行过公平模式，补发一次同步。
            OnLocalSettingsChanged(null, EventArgs.Empty);
            logger?.LogInformation("已注册 RemoteCI 插件设置页：IslandCaller");
        }
        catch (Exception ex)
        {
            // 插件设置页注册失败不影响随机点名扩展。
            logger?.LogWarning(ex, "注册 RemoteCI 插件设置页失败");
        }
    }

    private static void OnLocalSettingsChanged(object? sender, EventArgs e) =>
        _notifyTimer?.Change(NotifyDelay, Timeout.InfiniteTimeSpan);

    /// <summary>通知 RemoteCI 重新读取当前设置并同步给服务端，使 WebUI 预填值保持最新。</summary>
    private static void NotifySettingsChanged()
    {
        object? registry;
        Type? registryType;
        lock (SyncRoot)
        {
            if (!_groupRegistered) return;
            registry = _registry;
            registryType = _registryType;
        }

        try
        {
            registryType?.GetMethod("NotifySettingsChanged")?.Invoke(registry, new object[] { GroupId });
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "通知 RemoteCI 同步插件设置失败");
        }
    }

    private static void TryInvokeRegistry(string methodName, string id, ILogger? logger)
    {
        try
        {
            _registryType!.GetMethod(methodName)?.Invoke(_registry, new object[] { id });
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"注销 RemoteCI 扩展失败（{id}）：{ex}");
        }
    }

    /// <summary>在已加载程序集中查找 RemoteCI 插件程序集（插件加载完成后必然可见）。</summary>
    private static Assembly? FindRemoteCiAssembly() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, RemoteCiPluginAssemblyName, StringComparison.Ordinal));

    /// <summary>反射调用 IAppHost.GetService&lt;IRemoteCiExtensionRegistry&gt;() 获取注册表服务。</summary>
    private static object? GetRegistryService(Type registryType)
    {
        var method = typeof(IAppHost)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethodDefinition);
        if (method is null) return null;

        return method.MakeGenericMethod(registryType).Invoke(null, null);
    }
}

/// <summary>
/// RemoteCI 接口代理的公共部分：按 RemoteCI 的 CommandResult 结构反射构造回执，
/// 并把在 UI 线程上执行的操作包装成 RemoteCI 需要的 Task&lt;CommandResult&gt;。
/// </summary>
internal abstract class RemoteCiProxyBase : DispatchProxy
{
    private static readonly MethodInfo ToTypedTaskMethod = typeof(RemoteCiProxyBase)
        .GetMethod(nameof(ToTypedTaskAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    protected Type CommandResultType { get; set; } = null!;
    protected ILogger? Logger { get; set; }

    /// <summary>
    /// 把操作整体投递到 UI 线程执行，并在结果产生后再回填回执：
    /// 既不阻塞 RemoteCI 的命令处理线程（其超时依旧生效），也不会提前回执成功。
    /// 异常交给 RemoteCI 执行端统一转换为 INTERNAL_ERROR 回执。
    /// </summary>
    protected Task<RemoteCommandOutcome> RunOnUiThreadAsync(Func<RemoteCommandOutcome> action, string actionName)
    {
        var completion = new TaskCompletionSource<RemoteCommandOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "RemoteCI 扩展执行失败（{Action}）", actionName);
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    /// <summary>把内部回执转换为 RemoteCI 的 Task&lt;CommandResult&gt;。</summary>
    protected object ToCommandResultTask(Task<RemoteCommandOutcome> outcome) =>
        ToTypedTaskMethod.MakeGenericMethod(CommandResultType).Invoke(this, new object[] { outcome })!;

    /// <summary>按 RemoteCI 的 CommandResult 结构反射构造执行结果。</summary>
    protected object CreateResult(RemoteCommandOutcome outcome)
    {
        var result = Activator.CreateInstance(CommandResultType)!;
        CommandResultType.GetProperty("Success")!.SetValue(result, outcome.Success);
        CommandResultType.GetProperty("Code")!.SetValue(result, outcome.Code);
        CommandResultType.GetProperty("Message")!.SetValue(result, outcome.Message);
        return result;
    }

    private async Task<T> ToTypedTaskAsync<T>(Task<RemoteCommandOutcome> outcome) =>
        (T)CreateResult(await outcome.ConfigureAwait(false));
}

/// <summary>
/// 通过 DispatchProxy 动态实现 RemoteCI 的 IRemoteCiExtension 接口，
/// 使 IslandCaller 不需要在编译期引用 RemoteCI 的任何类型。
/// 注意：DispatchProxy 会在运行时生成该类的子类，因此此类不能声明为 sealed。
/// </summary>
internal class RemoteCiExtensionProxy : RemoteCiProxyBase
{
    public RemoteCiExtensionProxy()
    {
    }

    public static object Create(Type extensionInterfaceType, ILogger? logger)
    {
        var proxy = (RemoteCiExtensionProxy)DispatchProxy.Create(extensionInterfaceType, typeof(RemoteCiExtensionProxy));
        proxy.Logger = logger;

        // ExecuteAsync 返回 Task<CommandResult>，取出 CommandResult 类型用于反射构造结果。
        var executeMethod = extensionInterfaceType.GetMethod("ExecuteAsync")!;
        proxy.CommandResultType = executeMethod.ReturnType.GetGenericArguments()[0];
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;
        switch (targetMethod.Name)
        {
            case "get_Id":
                return RemoteCiBridge.ExtensionId;
            case "get_DisplayName":
                return "随机点名";
            case "get_RequiredPermission":
                // RemoteCI 扩展统一使用 RunExtensions 权限鉴权。
                return Enum.Parse(targetMethod.ReturnType, "RunExtensions");
            case "get_Icon":
                return RemoteCiBridge.WatchIconName;
            case "get_Parameters":
                // 无参数扩展：返回空数组即可满足 IReadOnlyList<ExtensionParameter>。
                return Array.CreateInstance(targetMethod.ReturnType.GetGenericArguments()[0], 0);
            case "get_Description":
                return "在教室端触发一次单人随机点名，结果由 IslandCaller 展示与播报。";
            case "get_GroupId":
                // 归入 IslandCaller 插件分组，WebUI 中与插件设置页显示在一起。
                return RemoteCiBridge.IsGroupRegistered ? RemoteCiBridge.GroupId : null;
            case "ExecuteAsync":
                // 准入判断与实际点名必须落在同一次 UI 线程调度里完成：ShowRandomStudent 在后台线程被调用时
                // 只是把执行投递到 UI 线程，若先判断再投递，两个并发请求之间或一次课间切换之后，
                // 判断结果就会失效，出现「回执成功但实际没有点名」。
                return ToCommandResultTask(RunOnUiThreadAsync(InvokeRandomCallOnUiThread, "随机点名"));
            default:
                Logger?.LogWarning($"RemoteCI 扩展调用了未处理的成员：{targetMethod.Name}");
                return null;
        }
    }

    /// <summary>
    /// 在 UI 线程上完成「准入判断 + 触发点名」，并返回 RemoteCI 的执行结果。
    /// 判断条件与 IslandCallerService.ShowRandomStudent 的准入条件保持一致：课间禁用期间、
    /// 或已有一次不可打断的点名正在进行时，点名不会真正执行，此时必须如实回执失败，
    /// 否则手表端与 WebUI 会收到「已开始随机点名」的错误回执。
    /// </summary>
    private RemoteCommandOutcome InvokeRandomCallOnUiThread()
    {
        var status = IAppHost.GetService<Status>();
        if (status.IsPluginReady)
        {
            // 触发一次单人随机抽选；展示与通知仍由 IslandCaller 自身完成。
            IAppHost.GetService<IslandCallerService>().ShowRandomStudent(1);
            return RemoteCommandOutcome.Ok("已开始随机点名");
        }

        string code;
        string message;
        if (!status.IsTimeStatusAvailable)
        {
            // 处于课间且启用了下课禁用，点名功能整体不可用。
            code = RemoteCommandOutcome.InvalidRequestCode;
            message = "当前处于课间，点名未执行";
        }
        else if (!status.OccupationDisable && !status.InterruptionEnable)
        {
            // 上一次点名仍未结束且不允许打断，对应 RemoteCI 的 BUSY 语义。
            code = RemoteCommandOutcome.BusyCode;
            message = "上一次点名尚未结束，点名未执行";
        }
        else
        {
            // 插件尚未完成初始化等其余未就绪状态。
            code = RemoteCommandOutcome.InvalidRequestCode;
            message = "IslandCaller 尚未就绪，点名未执行";
        }

        Logger?.LogInformation("RemoteCI 随机点名被拒绝（{Code}）：{Message}", code, message);
        return RemoteCommandOutcome.Failure(code, message);
    }
}

/// <summary>
/// 通过 DispatchProxy 动态实现 RemoteCI 的 IRemoteCiExtensionGroup 接口：
/// 在 WebUI 中注册 IslandCaller 分组与插件设置页，读写逻辑由 <see cref="RemoteSettingsService"/> 完成。
/// 注意：DispatchProxy 会在运行时生成该类的子类，因此此类不能声明为 sealed。
/// </summary>
internal class RemoteCiGroupProxy : RemoteCiProxyBase
{
    private RemoteSettingsService _remoteSettings = null!;
    private object _settingFields = null!;

    public RemoteCiGroupProxy()
    {
    }

    public static object Create(Type groupInterfaceType, RemoteSettingsService remoteSettings, ILogger? logger)
    {
        var proxy = (RemoteCiGroupProxy)DispatchProxy.Create(groupInterfaceType, typeof(RemoteCiGroupProxy));
        proxy.Logger = logger;
        proxy._remoteSettings = remoteSettings;

        var applyMethod = groupInterfaceType.GetMethod("ApplySettingsAsync")!;
        proxy.CommandResultType = applyMethod.ReturnType.GetGenericArguments()[0];

        // Settings 为 IReadOnlyList<ExtensionParameter>，从中取出 RemoteCI 的字段类型用于反射构造。
        var parameterType = groupInterfaceType.GetProperty("Settings")!.PropertyType.GetGenericArguments()[0];
        proxy._settingFields = RemoteCiFieldMapper.CreateList(parameterType, RemoteSettingsService.Fields);
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;
        switch (targetMethod.Name)
        {
            case "get_Id":
                return RemoteCiBridge.GroupId;
            case "get_DisplayName":
                return RemoteCiBridge.GroupDisplayName;
            case "get_Description":
                return "随机点名插件：远程编辑或上传名单、统一点名样式，可开启公平模式或锁定本机设置页。";
            case "get_Icon":
                return RemoteCiBridge.WatchIconName;
            case "get_Settings":
                return _settingFields;
            case "GetSettings":
                return _remoteSettings.GetValues();
            case "ApplySettingsAsync":
            {
                var values = new Dictionary<string, string?>(
                    args?[1] as IReadOnlyDictionary<string, string?> ?? new Dictionary<string, string?>(),
                    StringComparer.Ordinal);
                string? requestedBy = DescribeRequester(args?[0]);
                return ToCommandResultTask(RunOnUiThreadAsync(
                    () => _remoteSettings.Apply(values, requestedBy),
                    "修改插件设置"));
            }
            default:
                Logger?.LogWarning($"RemoteCI 扩展分组调用了未处理的成员：{targetMethod.Name}");
                return null;
        }
    }

    /// <summary>从 ExtensionExecutionContext.RequestedBy 中取出发起人，用于审计日志。</summary>
    private static string? DescribeRequester(object? context)
    {
        try
        {
            var user = context?.GetType().GetProperty("RequestedBy")?.GetValue(context);
            if (user is null) return null;
            var userType = user.GetType();
            var displayName = userType.GetProperty("DisplayName")?.GetValue(user) as string;
            var username = userType.GetProperty("Username")?.GetValue(user) as string;
            return string.IsNullOrWhiteSpace(displayName) ? username : $"{displayName}（{username}）";
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 把 <see cref="RemoteSettingField"/> 按属性名映射为 RemoteCI 的 ExtensionParameter。
/// 旧版 RemoteCI 不认识的布局属性（分区、卡片、联动等）直接跳过，退回平铺表单。
/// </summary>
internal static class RemoteCiFieldMapper
{
    public static object CreateList(Type parameterType, IReadOnlyList<RemoteSettingField> fields)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(parameterType))!;
        var typeProperty = parameterType.GetProperty("Type")!;
        var conditionType = parameterType.GetProperty("EnabledWhen")?.PropertyType.GetGenericArguments().FirstOrDefault();
        bool supportsCards = parameterType.GetProperty("Card") is not null;

        foreach (var field in fields)
        {
            var parameter = Activator.CreateInstance(parameterType)!;
            // 不支持卡片的旧版 RemoteCI 把卡片名拼进字段名，保留分组语义。
            string label = !supportsCards && field.Card is not null ? $"{field.Card} · {field.Label}" : field.Label;
            Set(parameter, "Key", field.Key);
            Set(parameter, "Label", label);
            typeProperty.SetValue(parameter, Enum.Parse(typeProperty.PropertyType, field.Type.ToString()));
            Set(parameter, "Required", field.Required);
            Set(parameter, "Description", field.Description);
            Set(parameter, "Placeholder", field.Placeholder);
            Set(parameter, "Multiline", field.Multiline);
            Set(parameter, "Options", field.Options?.ToList());
            Set(parameter, "OptionLabels", field.OptionLabels?.ToList());
            Set(parameter, "Min", field.Min);
            Set(parameter, "Max", field.Max);
            Set(parameter, "Step", field.Step);
            Set(parameter, "Slider", field.Slider);
            Set(parameter, "Section", field.Section);
            Set(parameter, "Card", field.Card);
            Set(parameter, "CardDescription", field.CardDescription);
            Set(parameter, "CardToggle", field.CardToggle);
            if (field.EnabledWhen is { } conditions && conditionType is not null)
            {
                Set(parameter, "EnabledWhen", CreateConditions(conditionType, conditions));
            }

            list.Add(parameter);
        }

        return list;
    }

    private static object CreateConditions(Type conditionType, IReadOnlyList<RemoteSettingCondition> conditions)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(conditionType))!;
        foreach (var condition in conditions)
        {
            var item = Activator.CreateInstance(conditionType)!;
            Set(item, "Key", condition.Key);
            Set(item, "Values", condition.Values.ToList());
            Set(item, "Not", condition.Not);
            list.Add(item);
        }

        return list;
    }

    private static void Set(object target, string propertyName, object? value)
    {
        if (value is null) return;
        var property = target.GetType().GetProperty(propertyName);
        if (property is null || !property.CanWrite) return;
        property.SetValue(target, value);
    }
}
