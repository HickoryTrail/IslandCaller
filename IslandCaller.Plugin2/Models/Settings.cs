using System.Text.Json;
using IslandCaller.Services;

namespace IslandCaller.Models
{
    public class Settings(ProfileService profileService)
    {
        public static SettingsModel Instance { get; } = new SettingsModel();
        public ProfileService ProfileService { get; } = profileService;

        private static string GetSettingsDir()
        {
            string dir;
            if (OperatingSystem.IsWindows())dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IslandCaller");
            else
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "IslandCaller");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetSettingsFilePath() => Path.Combine(GetSettingsDir(), "settings.json");

        private static bool HasLegacyDefaultProfileFile()
        {
            string profilePath = Path.Combine(GetSettingsDir(), "Profile");
            return File.Exists(Path.Combine(profilePath, "Default.csv")) || File.Exists(Path.Combine(profilePath, "default.csv"));
        }

        private static void CleanupLegacyInstall()
        {
            string dir = GetSettingsDir();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }

        private void InitializeNewInstall()
        {
            ProfileService.CreateDemoProfile(Instance.Profile.DefaultProfile);
            Save();
            ClassIsland.Core.Controls.CommonTaskDialogs.ShowDialog("Welcome", "Welcome to IslandCaller 2.0");
        }

        public void Load()
        {
            string settingsFile = GetSettingsFilePath();
            if (File.Exists(settingsFile))
            {
                try
                {
                    string json = File.ReadAllText(settingsFile);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        Instance.General.BreakDisable = data.BreakDisable;
                        Instance.General.Interruptable = data.Interruptable;
                        Instance.Profile.ProfileNum = data.ProfileNum;
                        Instance.Profile.DefaultProfile = data.DefaultProfile;
                        Instance.Profile.IsPreferProfile = data.IsPreferProfile;
                        Instance.Profile.ProfileList = data.ProfileList ?? new Dictionary<Guid, string>();
                        Instance.Profile.ProfilePrefer = data.ProfilePrefer ?? new Dictionary<Guid, string>();
                        Instance.Hover.IsEnable = data.HoverIsEnable;
                        Instance.Hover.ScalingFactor = data.HoverScalingFactor;
                        Instance.Hover.Position.X = data.HoverPositionX;
                        Instance.Hover.Position.Y = data.HoverPositionY;}
                }
                catch { InitializeNewInstall(); }
            }
            else
            {
                if (HasLegacyDefaultProfileFile()) CleanupLegacyInstall();
                InitializeNewInstall();
            }
            SettingsBinder.Bind(Instance, Save);
        }

        public void Save()
        {
            var data = new SettingsData
            {
                BreakDisable = Instance.General.BreakDisable,
                Interruptable = Instance.General.Interruptable,
                ProfileNum = Instance.Profile.ProfileNum,
                DefaultProfile = Instance.Profile.DefaultProfile,
                IsPreferProfile = Instance.Profile.IsPreferProfile,
                ProfileList = Instance.Profile.ProfileList,
                ProfilePrefer = Instance.Profile.ProfilePrefer,
                HoverIsEnable = Instance.Hover.IsEnable,
                HoverScalingFactor = Instance.Hover.ScalingFactor,
                HoverPositionX = Instance.Hover.Position.X,
                HoverPositionY = Instance.Hover.Position.Y
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            string dir = Path.GetDirectoryName(GetSettingsFilePath())!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(GetSettingsFilePath(), json);
        }

        private class SettingsData
        {
            public bool BreakDisable { get; set; } = true;
            public bool Interruptable { get; set; }
            public int ProfileNum { get; set; } = 1;
            public Guid DefaultProfile { get; set; }
            public bool IsPreferProfile { get; set; }
            public Dictionary<Guid, string> ProfileList { get; set; } = new();
            public Dictionary<Guid, string> ProfilePrefer { get; set; } = new();
            public bool HoverIsEnable { get; set; } = true;
            public double HoverScalingFactor { get; set; } = 1.0;
            public double HoverPositionX { get; set; } = 200.0;
            public double HoverPositionY { get; set; } = 200.0;
        }
    }

    public static class SettingsBinder
    {
        public static void Bind(SettingsModel model, Action onChange)
        {
            model.General.PropertyChanged += (_, _) => onChange();
            model.Hover.PropertyChanged += (_, _) => onChange();
            model.Hover.Position.PropertyChanged += (_, _) => onChange();
        }
    }
}