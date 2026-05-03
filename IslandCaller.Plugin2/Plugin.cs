using Avalonia.Controls;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using IslandCaller.Actions;
using IslandCaller.Helpers;
using IslandCaller.Models;
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
        public Window HoverWindow { get; set; }

        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            var logger = IAppHost.TryGetService<ILogger<Plugin>>();
            services.AddSingleton<Status>();
            services.AddNotificationProvider<IslandCallerNotificationProviderNew>();
            services.AddSingleton<IslandCallerService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<HistoryService>();
            services.AddSingleton<CoreService>();
            services.AddSingleton<WindowDragHelper>();
            services.AddSingleton<WindowTopmostHelper>();
            services.AddSettingsPage<SettingPage>();
            services.AddAction<DisableHoverAction>();
            services.AddAction<EnableHoverAction>();
            services.AddAction<CallAction>();
            AppBase.Current.AppStarted += async (_, _) =>
            {
                try
                {
                    logger = IAppHost.GetService<ILogger<Plugin>>();
                    IAppHost.GetService<Status>();
                    logger.LogInformation("插件状态初始化完成，正在加载设置...");
                    new Settings(IAppHost.GetService<ProfileService>()).Load();
                    logger.LogDebug("设置加载完成，正在加载默认配置...");
                    IAppHost.GetService<ProfileService>().LoadSelectedProfile(Settings.Instance.Profile.DefaultProfile);
                    logger.LogDebug("默认配置加载完成，正在加载历史记录...");
                    IAppHost.GetService<HistoryService>().Load(Settings.Instance.Profile.DefaultProfile);
                    logger.LogDebug("历史记录加载完成，正在初始化核心服务...");
                    IAppHost.GetService<CoreService>().InitializeCore();
                    logger.LogDebug("核心服务初始化完成，正在启动 IslandCaller 服务...");
                    IAppHost.GetService<IslandCallerService>();
                    logger.LogInformation("IslandCaller 插件初始化完成");
                    if (Settings.Instance.Hover.IsEnable)
                    {
                        HoverWindow = new HoverFluent();
                        HoverWindow.Show();
                    }
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