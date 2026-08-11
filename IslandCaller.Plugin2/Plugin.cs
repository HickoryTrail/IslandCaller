using Avalonia.Controls;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using IslandCaller.Actions;
using IslandCaller.Helpers;
using IslandCaller.Models;
using IslandCaller.Plugin2;
using IslandCaller.Plugin2.Services;
using IslandCaller.Services;
using IslandCaller.Services.IslandCallerService;
using IslandCaller.Services.NotificationProvidersNew;
using IslandCaller.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

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
            services.AddSingleton<WindowsManager>();
            services.AddSingleton<WindowDragHelper>();
            services.AddSingleton<WindowSizeHelper>();
            services.AddSingleton<WindowTopmostHelper>();
            services.AddSingleton<ScreenBrightnessHelper>();
            services.AddSettingsPage<SettingPage>();
            services.AddAction<DisableHoverAction>();
            services.AddAction<EnableHoverAction>();
            services.AddAction<CallAction>();
            AppBase.Current.AppStarted += async (_, _) =>
            {
                try
                {
                    logger = IAppHost.TryGetService<ILogger<Plugin>>();
                    IAppHost.GetService<Status>();
                    logger?.LogInformation("插件状态初始化完成，正在加载设置...");
                    new Settings(IAppHost.GetService<ProfileService>()).Load();
                    logger?.LogDebug("设置加载完成，正在加载默认配置...");
                    IAppHost.GetService<ProfileService>().Initialize();
                    IAppHost.GetService<HistoryService>().Initialize();
                    IAppHost.GetService<CoreService>().Initialize();
                    IAppHost.GetService<IslandCallerService>().Initialize();
                    IAppHost.GetService<WindowsManager>().Initialize();
                }
                catch (Exception ex)
                {
                    logger = IAppHost.GetService<ILogger<Plugin>>();
                    logger.LogCritical($"初始化失败：{ex}");
                    throw;
                }

            };
        }
    }
}
