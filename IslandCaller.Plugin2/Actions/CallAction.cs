using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using IslandCaller.Services.IslandCallerService;
using Microsoft.Extensions.Logging;

namespace IslandCaller.Actions
{
    [ActionInfo("IslandCaller.Call", "随机点名", "\uECF9", false)]
    public class CallAction(ILogger<CallAction> logger) : ActionBase
    {
        private readonly ILogger<CallAction> _logger = logger;
        private readonly IslandCallerService _islandCallerService = IAppHost.GetService<IslandCallerService>();
        protected override async Task OnInvoke()
        {
            await base.OnInvoke();
            _logger.LogInformation("行动：随机点名");
            _islandCallerService.ShowRandomStudent(1);
        }
    }
}
