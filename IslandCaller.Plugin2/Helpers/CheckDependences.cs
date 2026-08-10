using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Plugin;
using ClassIsland.Shared;
using OmniTTS.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace IslandCaller.Plugin2.Helpers
{
    internal static class CheckDependences
    {
        internal static bool CheckOmniTTS()
        {
            var omniTTService = IAppHost.TryGetService<IOmniTTS>();
            if (omniTTService != null) return true;
            else return false;
        }
    }
}
