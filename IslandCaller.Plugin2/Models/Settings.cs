using Microsoft.Win32;
using System.Text.Json;
using IslandCaller.Services;

namespace IslandCaller.Models
{
    public class Settings(ProfileService profileService)
    {
        public static SettingsModel Instance { get; } = new SettingsModel();
        public ProfileService ProfileService { get; } = profileService;

        private static string GetAppDataRootPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IslandCaller"
            );
        }

        private static bool HasLegacyDefaultProfileFile()
        {
            string profilePath = Path.Combine(GetAppDataRootPath(), "Profile");
            return File.Exists(Path.Combine(profilePath, "Default.csv")) ||
                   File.Exists(Path.Combine(profilePath, "default.csv"));
        }

        private static void CleanupLegacyInstall()
        {
            string appDataRootPath = GetAppDataRootPath();

            if (Directory.Exists(appDataRootPath))
            {
                Directory.Delete(appDataRootPath, recursive: true);
            }

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\IslandCaller", throwOnMissingSubKey: false);
        }

        private void InitializeNewInstall()
        {
            RegistryKey IsC_RootKey = Registry.CurrentUser.CreateSubKey(@"Software\IslandCaller", writable: true);
            RegistryKey IsC_GeneralKey = IsC_RootKey?.CreateSubKey("General", writable: true);
            RegistryKey IsC_ProfileKey = IsC_RootKey?.CreateSubKey("Profile", writable: true);
            RegistryKey IsC_HoverKey = IsC_RootKey?.CreateSubKey("Hover", writable: true);
            RegistryKey IsC_HoverKey_Position = IsC_HoverKey?.CreateSubKey("Position", writable: true);

            IsC_GeneralKey?.SetValue("BreakDisable", Instance.General.BreakDisable);
            IsC_GeneralKey?.SetValue("Interruptable", Instance.General.Interruptable);
            IsC_ProfileKey?.SetValue("ProfileNum", Instance.Profile.ProfileNum);
            IsC_ProfileKey?.SetValue("DefaultProfileName", Instance.Profile.DefaultProfile.ToString());
            IsC_ProfileKey?.SetValue("IsPreferProfile", Instance.Profile.IsPreferProfile);
            IsC_ProfileKey?.SetValue("ProfileList", JsonSerializer.Serialize(Instance.Profile.ProfileList));
            IsC_ProfileKey?.SetValue("PreferProfile", JsonSerializer.Serialize(Instance.Profile.ProfilePrefer));
            IsC_HoverKey?.SetValue("IsEnable", Instance.Hover.IsEnable);
            IsC_HoverKey?.SetValue("ScalingFactor", Instance.Hover.ScalingFactor);
            IsC_HoverKey_Position?.SetValue("X", Instance.Hover.Position.X);
            IsC_HoverKey_Position?.SetValue("Y", Instance.Hover.Position.Y);

            ProfileService.CreateDemoProfile(Instance.Profile.DefaultProfile);
            ClassIsland.Core.Controls.CommonTaskDialogs.ShowDialog("Welcome", "欢迎使用IslandCaller2.0");
        }

        public void Load()
        {
            RegistryKey IsC_RootKey = Registry.CurrentUser.OpenSubKey(@"Software\IslandCaller", writable: true);
            RegistryKey IsC_GeneralKey;
            RegistryKey IsC_ProfileKey;
            RegistryKey IsC_HoverKey;
            RegistryKey IsC_HoverKey_Position;

            if (IsC_RootKey == null)
            {
                InitializeNewInstall();
            }
            else
            {
                if (HasLegacyDefaultProfileFile())
                {
                    CleanupLegacyInstall();
                    InitializeNewInstall();
                    SettingsBinder.Bind(Instance, Save);
                    return;
                }

                IsC_GeneralKey = IsC_RootKey?.OpenSubKey("General", writable: true);
                IsC_ProfileKey = IsC_RootKey?.OpenSubKey("Profile", writable: true);
                IsC_HoverKey = IsC_RootKey?.OpenSubKey("Hover", writable: true);
                IsC_HoverKey_Position = IsC_HoverKey?.OpenSubKey("Position", writable: true);

                Instance.General.BreakDisable = Convert.ToBoolean(IsC_GeneralKey?.GetValue("BreakDisable") ?? true);
                Instance.General.Interruptable = Convert.ToBoolean(IsC_GeneralKey?.GetValue("Interruptable") ?? false);
                Instance.Profile.ProfileNum = Convert.ToInt32(IsC_ProfileKey?.GetValue("ProfileNum"));
                Instance.Profile.DefaultProfile = Guid.Parse(IsC_ProfileKey?.GetValue("DefaultProfileName") as string);
                Instance.Profile.IsPreferProfile = Convert.ToBoolean(IsC_ProfileKey?.GetValue("IsPreferProfile") ?? false);
                Instance.Profile.ProfileList = JsonSerializer.Deserialize<Dictionary<Guid, string>>((IsC_ProfileKey?.GetValue("ProfileList") ?? "{}") as string);
                Instance.Profile.ProfilePrefer = JsonSerializer.Deserialize<Dictionary<Guid, string>>((IsC_ProfileKey?.GetValue("PreferProfile") ?? "{}") as string);
                Instance.Hover.IsEnable = Convert.ToBoolean(IsC_HoverKey?.GetValue("IsEnable") ?? true);
                Instance.Hover.ScalingFactor = Convert.ToDouble(IsC_HoverKey?.GetValue("ScalingFactor") ?? 1.0);
                Instance.Hover.Position.X = Convert.ToDouble(IsC_HoverKey_Position?.GetValue("X") ?? 200.0);
                Instance.Hover.Position.Y = Convert.ToDouble(IsC_HoverKey_Position?.GetValue("Y") ?? 200.0);
                Save();
            }

            SettingsBinder.Bind(Instance, Save);
        }

        public void Save()
        {
            RegistryKey IsC_RootKey = Registry.CurrentUser.OpenSubKey(@"Software\IslandCaller", writable: true);
            RegistryKey IsC_GeneralKey = IsC_RootKey?.OpenSubKey("General", writable: true);
            RegistryKey IsC_ProfileKey = IsC_RootKey?.OpenSubKey("Profile", writable: true);
            RegistryKey IsC_HoverKey = IsC_RootKey?.OpenSubKey("Hover", writable: true);
            RegistryKey IsC_HoverKey_Position = IsC_HoverKey?.OpenSubKey("Position", writable: true);

            IsC_GeneralKey?.SetValue("BreakDisable", Instance.General.BreakDisable);
            IsC_GeneralKey?.SetValue("Interruptable", Instance.General.Interruptable);
            IsC_ProfileKey?.SetValue("ProfileNum", Instance.Profile.ProfileNum);
            IsC_ProfileKey?.SetValue("DefaultProfileName", Instance.Profile.DefaultProfile.ToString());
            IsC_ProfileKey?.SetValue("IsPreferProfile", Instance.Profile.IsPreferProfile);
            IsC_ProfileKey?.SetValue("ProfileList", JsonSerializer.Serialize(Instance.Profile.ProfileList));
            IsC_ProfileKey?.SetValue("PreferProfile", JsonSerializer.Serialize(Instance.Profile.ProfilePrefer));
            IsC_HoverKey?.SetValue("IsEnable", Instance.Hover.IsEnable);
            IsC_HoverKey?.SetValue("ScalingFactor", Instance.Hover.ScalingFactor);
            IsC_HoverKey_Position?.SetValue("X", Instance.Hover.Position.X);
            IsC_HoverKey_Position?.SetValue("Y", Instance.Hover.Position.Y);
        }
    }
    public static class SettingsBinder
    {
        public static void Bind(SettingsModel model, Action onChange)
        {
            // General
            model.General.PropertyChanged += (_, _) => onChange();

            // Hover
            model.Hover.PropertyChanged += (_, _) => onChange();
            model.Hover.Position.PropertyChanged += (_, _) => onChange();
        }
    }

}
