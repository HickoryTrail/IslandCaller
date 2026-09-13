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

namespace IslandCaller
{
    [PluginEntrance]
    public class Plugin : PluginBase
    {
        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            var logger = IAppHost.TryGetService<ILogger<Plugin>>();
            services.AddSingleton<Status>();
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
                    // 接入 RemoteCI：在手表“控制”页注册“随机点名”远程扩展（RemoteCI 未安装时自动跳过）。
                    try
                    {
                        RemoteCiBridge.RegisterRandomCallExtension(logger);
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

            // RemoteCI 插件退出时注销远程扩展，避免残留无效入口。
            AppBase.Current.AppStopping += (_, _) =>
            {
                try
                {
                    RemoteCiBridge.UnregisterRandomCallExtension(logger);
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
    }
}
