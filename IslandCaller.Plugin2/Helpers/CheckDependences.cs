using ClassIsland.Shared;
using OmniTTS.Shared;

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
