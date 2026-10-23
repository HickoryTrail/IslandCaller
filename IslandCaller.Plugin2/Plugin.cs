using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Models.Automation;
using ClassIsland.Shared;
using IslandCaller.Actions;
using IslandCaller.Controls;
using IslandCaller.Extensions;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Services;
using IslandCaller.Services.IslandCallerService;
using IslandCaller.Services.NotificationProvidersNew;
using IslandCaller.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperAutoIsland.Interface.Metadata;
using SuperAutoIsland.Interface.Services;
using System.Runtime.CompilerServices;

namespace IslandCaller
{
    [PluginEntrance]
    public class Plugin : PluginBase
    {
        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            var logger = IAppHost.TryGetService<ILogger<Plugin>>();
            services.AddSingleton<Status>();
            services.AddSingleton<RemotePolicyService>();
            services.AddSingleton<RemoteSettingsService>();
            services.AddNotificationProvider<IslandCallerNotificationProviderNew>();
            services.AddSingleton<IslandCallerService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<HistoryService>();
            services.AddSingleton<CoreService>();
            services.AddSingleton<ProfileRuntimeService>();
            services.AddSingleton<WindowsManager>();
            services.AddSingleton<LiquidGlassRuntime>();
            services.AddSingleton<WindowDragHelper>();
            services.AddSingleton<WindowSizeHelper>();
            services.AddSingleton<WindowTopmostHelper>();
            services.AddSingleton<ScreenBrightnessHelper>();
            services.AddSettingsPage<SettingPage>();
            BuildActionMenu();
            services.AddAction<DisableHoverAction>();
            services.AddAction<EnableHoverAction>();
            services.AddAction<CallAction>();
            services.AddAction<SwitchProfileAction, SwitchProfileActionSettingsControl>();
            AppBase.Current.AppStarted += async (_, _) =>
            {
                try
                {
                    logger = IAppHost.TryGetService<ILogger<Plugin>>();
                    IAppHost.GetService<Status>();
                    logger?.LogInformation("插件状态初始化完成，正在加载设置...");
                    new Settings(IAppHost.GetService<ProfileService>()).Load();
                    await IAppHost.GetService<LiquidGlassRuntime>().PrewarmAsync();
                    logger?.LogDebug("设置加载完成，正在加载默认配置...");
                    IAppHost.GetService<ProfileRuntimeService>().Initialize();
                    IAppHost.GetService<IslandCallerService>().Initialize();
                    IAppHost.GetService<WindowsManager>().Initialize();
                    RegisterSuperAutoIslandBlocks(logger);
                    // 接入 RemoteCI：注册 IslandCaller 插件设置页与“随机点名”远程扩展（RemoteCI 未安装时自动跳过）。
                    try
                    {
                        RemoteCiBridge.Register(logger);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "注册 RemoteCI 远程扩展失败");
                    }
                }
                catch (Exception ex)
                {
                    logger = IAppHost.GetService<ILogger<Plugin>>();
                    logger.LogCritical($"初始化失败：{ex}");
                    throw;
                }

            };

            // 退出时注销远程扩展与插件设置页，避免残留无效入口。
            AppBase.Current.AppStopping += (_, _) =>
            {
                try
                {
                    RemoteCiBridge.Unregister(logger);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning($"注销 RemoteCI 扩展失败：{ex}");
                }
            };
        }

        private static void BuildActionMenu()
        {
            IActionService.ActionMenuTree.Add(new ActionMenuTreeGroup("IslandCaller 行动", "\uECF9"));
            IActionService.ActionMenuTree["IslandCaller 行动"].Add(
                new ActionMenuTreeItem("IslandCaller.Call", "随机点名", "\uECF9"));
            IActionService.ActionMenuTree["IslandCaller 行动"].Add(
                new ActionMenuTreeItem("IslandCaller.EnableHover", "启用悬浮窗", "\uF484"));
            IActionService.ActionMenuTree["IslandCaller 行动"].Add(
                new ActionMenuTreeItem("IslandCaller.DisableHover", "禁用悬浮窗", "\uF486"));
            IActionService.ActionMenuTree["IslandCaller 行动"].Add(
                new ActionMenuTreeItem("IslandCaller.SwitchProfile", "切换档案", "\uE9A8"));
        }

        private static void RegisterSuperAutoIslandBlocks(ILogger<Plugin>? logger)
        {
            if (!IPluginService.LoadedPlugins.Any(info => info.Manifest.Id == "lrs2187.sai"))
            {
                return;
            }

            try
            {
                RegisterSuperAutoIslandBlocksCore(logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "注册 SuperAutoIsland 积木失败");
            }
        }

        // 含 SuperAutoIsland 类型引用的代码必须独立成方法：JIT 编译该方法时才会加载 SAI 程序集，
        // 若与外层判断写在一起，SAI 版本不匹配会在判断与 try/catch 生效之前就抛出并导致插件被禁用。
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterSuperAutoIslandBlocksCore(ILogger<Plugin>? logger)
        {
            IAppHost.GetService<ISaiServer>().RegisterBlocks("IslandCaller", blocks => blocks
                .AddBlock(new BlockMetadata("IslandCaller.Call")
                {
                    Kind = BlockKind.Action,
                    Name = "随机点名",
                    Icon = ("点名", "\uECF9"),
                })
                .AddBlock(new BlockMetadata("IslandCaller.EnableHover")
                {
                    Kind = BlockKind.Action,
                    Name = "显示悬浮窗",
                    Icon = ("显示", "\uF484"),
                })
                .AddBlock(new BlockMetadata("IslandCaller.DisableHover")
                {
                    Kind = BlockKind.Action,
                    Name = "隐藏悬浮窗",
                    Icon = ("隐藏", "\uF486"),
                }));

            logger?.LogInformation("已注册 SuperAutoIsland 积木：随机点名、显示悬浮窗、隐藏悬浮窗");
        }
    }
}
