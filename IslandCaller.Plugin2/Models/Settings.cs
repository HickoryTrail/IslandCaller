using Microsoft.Win32;
using System.Text.Json;
using IslandCaller.Services;
using IslandCaller;

namespace IslandCaller.Models
{
    public class Settings(ProfileService profileService)
    {
        public static SettingsModel Instance { get; private set; } = new SettingsModel();
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
            RegistryKey IsC_TTSKey = IsC_RootKey?.CreateSubKey("TTS", writable: true);
            RegistryKey IsC_CallKey = IsC_RootKey?.CreateSubKey("Call", writable: true);
            RegistryKey IsC_SecurityKey = IsC_RootKey?.CreateSubKey("Security", writable: true);
            RegistryKey IsC_AlgorithmKey = IsC_RootKey?.CreateSubKey("Algorithm", writable: true);
            RegistryKey IsC_AppearanceKey = IsC_RootKey?.CreateSubKey("Appearance", writable: true);
            RegistryKey IsC_TopmostKey = IsC_RootKey?.CreateSubKey("Topmost", writable: true);
            RegistryKey IsC_HotkeyKey = IsC_RootKey?.CreateSubKey("Hotkey", writable: true);

            IsC_GeneralKey?.SetValue("BreakDisable", Instance.General.BreakDisable);
            IsC_GeneralKey?.SetValue("Interruptable", Instance.General.Interruptable);
            IsC_ProfileKey?.SetValue("ProfileNum", Instance.Profile.ProfileNum);
            IsC_ProfileKey?.SetValue("DefaultProfileName", Instance.Profile.DefaultProfile.ToString());
            IsC_ProfileKey?.SetValue("IsPreferProfile", Instance.Profile.IsPreferProfile);
            IsC_ProfileKey?.SetValue("ProfileList", JsonSerializer.Serialize(Instance.Profile.ProfileList));
            IsC_ProfileKey?.SetValue("PreferProfile", JsonSerializer.Serialize(Instance.Profile.ProfilePrefer));
            IsC_HoverKey?.SetValue("IsEnable", Instance.Hover.IsEnable);
            IsC_HoverKey?.SetValue("ScalingFactor", Instance.Hover.ScalingFactor);
            IsC_HoverKey?.SetValue("HoverLayout", Instance.Hover.HoverLayout);
            IsC_HoverKey?.SetValue("LayoutDirection", Instance.Hover.LayoutDirection);
            IsC_HoverKey?.SetValue("CallButtonWidth", Instance.Hover.CallButtonWidth);
            IsC_HoverKey?.SetValue("SnapToScreenEdge", Instance.Hover.SnapToScreenEdge);
            IsC_HoverKey?.SetValue("HoverTheme", Instance.Hover.HoverTheme);
            IsC_HoverKey_Position?.SetValue("X", Instance.Hover.Position.X);
            IsC_HoverKey_Position?.SetValue("Y", Instance.Hover.Position.Y);
            IsC_TTSKey?.SetValue("BeforeText", Instance.TTS.BeforeText);
            IsC_TTSKey?.SetValue("AfterText", Instance.TTS.AfterText);
            IsC_TTSKey?.SetValue("Provider", Instance.TTS.Provider.ToString());
            IsC_CallKey?.SetValue("NotifyMethod", Instance.Call.NotifyMethod);
            IsC_CallKey?.SetValue("ShowerTheme", Instance.Call.ShowerTheme);
            IsC_CallKey?.SetValue("BaseTime", Instance.Call.BaseTime);
            IsC_CallKey?.SetValue("AdditionalTime", Instance.Call.AdditionalTime);
            IsC_CallKey?.SetValue("MaxDrawCount", Instance.Call.MaxDrawCount);
            IsC_SecurityKey?.SetValue("ViewPasswordHash", Instance.Security.ViewPasswordHash);
            IsC_SecurityKey?.SetValue("IsViewPasswordEnabled", Instance.Security.IsViewPasswordEnabled);
            IsC_SecurityKey?.SetValue("EditPasswordHash", Instance.Security.EditPasswordHash);
            IsC_SecurityKey?.SetValue("IsEditPasswordEnabled", Instance.Security.IsEditPasswordEnabled);
            IsC_AlgorithmKey?.SetValue("HalfRecoveryDistance", Instance.Algorithm.HalfRecoveryDistance);
            IsC_AlgorithmKey?.SetValue("CurvePower", Instance.Algorithm.CurvePower);
            IsC_AlgorithmKey?.SetValue("Gamma", Instance.Algorithm.Gamma);
            IsC_AlgorithmKey?.SetValue("RMin", Instance.Algorithm.RMin);
            IsC_AlgorithmKey?.SetValue("RMax", Instance.Algorithm.RMax);
            IsC_AppearanceKey?.SetValue("AccentColor", Instance.Appearance.AccentColor);
            IsC_AppearanceKey?.SetValue("ResultTextColor", Instance.Appearance.ResultTextColor);
            IsC_AppearanceKey?.SetValue("HoverText", Instance.Appearance.HoverText);
            IsC_AppearanceKey?.SetValue("HoverImagePath", Instance.Appearance.HoverImagePath);
            IsC_AppearanceKey?.SetValue("ResultImagePath", Instance.Appearance.ResultImagePath);
            IsC_AppearanceKey?.SetValue("FontFamily", Instance.Appearance.FontFamily);
            IsC_AppearanceKey?.SetValue("ResultFontSize", Instance.Appearance.ResultFontSize);
            IsC_AppearanceKey?.SetValue("ResultBackground", Instance.Appearance.ResultBackground);
            IsC_AppearanceKey?.SetValue("CornerRadius", Instance.Appearance.CornerRadius);
            IsC_AppearanceKey?.SetValue("KeepHoverColor", Instance.Appearance.KeepHoverColor);
            IsC_TopmostKey?.SetValue("Enabled", Instance.Topmost.Enabled);
            IsC_TopmostKey?.SetValue("IntervalMs", Instance.Topmost.IntervalMs);
            IsC_TopmostKey?.SetValue("EnableTopmostStyle", Instance.Topmost.EnableTopmostStyle);
            IsC_TopmostKey?.SetValue("EnableToolWindow", Instance.Topmost.EnableToolWindow);
            IsC_TopmostKey?.SetValue("EnableNoActivate", Instance.Topmost.EnableNoActivate);
            IsC_TopmostKey?.SetValue("EnableForegroundHook", Instance.Topmost.EnableForegroundHook);
            IsC_TopmostKey?.SetValue("EnableUiaDetection", Instance.Topmost.EnableUiaDetection);
            IsC_TopmostKey?.SetValue("ExtraTitleKeywords", JsonSerializer.Serialize(Instance.Topmost.ExtraTitleKeywords));
            IsC_HotkeyKey?.SetValue("Enabled", Instance.Hotkey.Enabled);
            IsC_HotkeyKey?.SetValue("Modifiers", (int)Instance.Hotkey.Modifiers);
            IsC_HotkeyKey?.SetValue("Key", Instance.Hotkey.Key);
            IsC_HotkeyKey?.SetValue("Scope", (int)Instance.Hotkey.Scope);

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
            RegistryKey IsC_TTSKey;
            RegistryKey IsC_CallKey;
            RegistryKey IsC_SecurityKey;
            RegistryKey IsC_AlgorithmKey;
            RegistryKey IsC_AppearanceKey;
            RegistryKey IsC_TopmostKey;
            RegistryKey IsC_HotkeyKey;

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
                IsC_TTSKey = IsC_RootKey?.OpenSubKey("TTS", writable: true) ?? IsC_RootKey?.CreateSubKey("TTS", writable: true);
                IsC_CallKey = IsC_RootKey?.OpenSubKey("Call", writable: true) ?? IsC_RootKey?.CreateSubKey("Call", writable: true);
                IsC_SecurityKey = IsC_RootKey?.OpenSubKey("Security", writable: true) ?? IsC_RootKey?.CreateSubKey("Security", writable: true);
                IsC_AlgorithmKey = IsC_RootKey?.OpenSubKey("Algorithm", writable: true) ?? IsC_RootKey?.CreateSubKey("Algorithm", writable: true);
                IsC_AppearanceKey = IsC_RootKey?.OpenSubKey("Appearance", writable: true) ?? IsC_RootKey?.CreateSubKey("Appearance", writable: true);
                IsC_TopmostKey = IsC_RootKey?.OpenSubKey("Topmost", writable: true) ?? IsC_RootKey?.CreateSubKey("Topmost", writable: true);
                IsC_HotkeyKey = IsC_RootKey?.OpenSubKey("Hotkey", writable: true) ?? IsC_RootKey?.CreateSubKey("Hotkey", writable: true);

                Instance.General.BreakDisable = Convert.ToBoolean(IsC_GeneralKey?.GetValue("BreakDisable") ?? true);
                Instance.General.Interruptable = Convert.ToBoolean(IsC_GeneralKey?.GetValue("Interruptable") ?? false);
                Instance.Profile.ProfileNum = Convert.ToInt32(IsC_ProfileKey?.GetValue("ProfileNum"));
                Instance.Profile.DefaultProfile = Guid.Parse(IsC_ProfileKey?.GetValue("DefaultProfileName") as string);
                Instance.Profile.IsPreferProfile = Convert.ToBoolean(IsC_ProfileKey?.GetValue("IsPreferProfile") ?? false);
                string profileListJson = IsC_ProfileKey?.GetValue("ProfileList") as string ?? "{}";
                string profilePreferJson = IsC_ProfileKey?.GetValue("PreferProfile") as string ?? "{}";
                Instance.Profile.ProfileList = JsonSerializer.Deserialize<Dictionary<Guid, string>>(profileListJson) ?? new Dictionary<Guid, string>();
                Instance.Profile.ProfilePrefer = JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(profilePreferJson) ?? new Dictionary<Guid, Guid>();
                Instance.Hover.IsEnable = Convert.ToBoolean(IsC_HoverKey?.GetValue("IsEnable") ?? true);
                Instance.Hover.ScalingFactor = Convert.ToDouble(IsC_HoverKey?.GetValue("ScalingFactor") ?? 1.0);
                Instance.Hover.HoverLayout = Convert.ToInt32(IsC_HoverKey?.GetValue("HoverLayout") ?? 0);
                Instance.Hover.LayoutDirection = Convert.ToInt32(IsC_HoverKey?.GetValue("LayoutDirection") ?? 0);
                Instance.Hover.CallButtonWidth = Convert.ToDouble(IsC_HoverKey?.GetValue("CallButtonWidth") ?? 88.0);
                Instance.Hover.SnapToScreenEdge = Convert.ToBoolean(IsC_HoverKey?.GetValue("SnapToScreenEdge") ?? false);
                Instance.Hover.HoverTheme = Convert.ToInt32(IsC_HoverKey?.GetValue("HoverTheme") ?? 0);
                Instance.Hover.Position.X = Convert.ToDouble(IsC_HoverKey_Position?.GetValue("X") ?? 200.0);
                Instance.Hover.Position.Y = Convert.ToDouble(IsC_HoverKey_Position?.GetValue("Y") ?? 200.0);
                Instance.TTS.BeforeText = IsC_TTSKey?.GetValue("BeforeText") as string ?? string.Empty;
                Instance.TTS.AfterText = IsC_TTSKey?.GetValue("AfterText") as string ?? string.Empty;
                Instance.TTS.Provider = ReadTtsProvider(IsC_TTSKey?.GetValue("Provider"));
                Instance.Call.NotifyMethod = Convert.ToInt32(IsC_CallKey?.GetValue("NotifyMethod") ?? 1);
                Instance.Call.ShowerTheme = Convert.ToInt32(IsC_CallKey?.GetValue("ShowerTheme") ?? 0);
                Instance.Call.BaseTime = Convert.ToSingle(IsC_CallKey?.GetValue("BaseTime") ?? 1.0f);
                Instance.Call.AdditionalTime = Convert.ToSingle(IsC_CallKey?.GetValue("AdditionalTime") ?? 2.0f);
                Instance.Call.MaxDrawCount = Convert.ToInt32(IsC_CallKey?.GetValue("MaxDrawCount") ?? 5);
                // 兼容旧键名 PasswordHash/IsEnabled：迁移到「查看密码」
                var legacyViewPasswordHash = IsC_SecurityKey?.GetValue("PasswordHash") as string;
                var legacyViewPasswordEnabled = IsC_SecurityKey?.GetValue("IsEnabled");
                Instance.Security.ViewPasswordHash = IsC_SecurityKey?.GetValue("ViewPasswordHash") as string
                    ?? legacyViewPasswordHash ?? string.Empty;
                Instance.Security.IsViewPasswordEnabled = Convert.ToBoolean(
                    IsC_SecurityKey?.GetValue("IsViewPasswordEnabled") ?? legacyViewPasswordEnabled ?? false);
                Instance.Security.EditPasswordHash = IsC_SecurityKey?.GetValue("EditPasswordHash") as string ?? string.Empty;
                Instance.Security.IsEditPasswordEnabled = Convert.ToBoolean(IsC_SecurityKey?.GetValue("IsEditPasswordEnabled") ?? false);
                Instance.Algorithm.HalfRecoveryDistance = Convert.ToDouble(IsC_AlgorithmKey?.GetValue("HalfRecoveryDistance") ?? 5.0);
                Instance.Algorithm.CurvePower = Convert.ToDouble(IsC_AlgorithmKey?.GetValue("CurvePower") ?? 6.0);
                Instance.Algorithm.Gamma = Convert.ToDouble(IsC_AlgorithmKey?.GetValue("Gamma") ?? 0.9);
                Instance.Algorithm.RMin = Convert.ToDouble(IsC_AlgorithmKey?.GetValue("RMin") ?? 0.6);
                Instance.Algorithm.RMax = Convert.ToDouble(IsC_AlgorithmKey?.GetValue("RMax") ?? 1.6);
                Instance.Appearance.AccentColor = IsC_AppearanceKey?.GetValue("AccentColor") as string ?? "#0078D4";
                Instance.Appearance.ResultTextColor = IsC_AppearanceKey?.GetValue("ResultTextColor") as string ?? string.Empty;
                Instance.Appearance.HoverText = IsC_AppearanceKey?.GetValue("HoverText") as string ?? "Call";
                Instance.Appearance.HoverImagePath = IsC_AppearanceKey?.GetValue("HoverImagePath") as string ?? string.Empty;
                Instance.Appearance.ResultImagePath = IsC_AppearanceKey?.GetValue("ResultImagePath") as string ?? string.Empty;
                Instance.Appearance.FontFamily = IsC_AppearanceKey?.GetValue("FontFamily") as string ?? "HarmonyOS Sans SC";
                Instance.Appearance.ResultFontSize = Convert.ToDouble(IsC_AppearanceKey?.GetValue("ResultFontSize") ?? 60);
                Instance.Appearance.ResultBackground = IsC_AppearanceKey?.GetValue("ResultBackground") as string ?? string.Empty;
                Instance.Appearance.CornerRadius = Convert.ToDouble(
                    IsC_AppearanceKey?.GetValue("CornerRadius") ?? AppearanceSetting.DefaultCornerRadius);
                Instance.Appearance.KeepHoverColor = Convert.ToBoolean(
                    IsC_AppearanceKey?.GetValue("KeepHoverColor") ?? false);
                Instance.Topmost.Enabled = Convert.ToBoolean(IsC_TopmostKey?.GetValue("Enabled") ?? true);
                Instance.Topmost.IntervalMs = Convert.ToInt32(IsC_TopmostKey?.GetValue("IntervalMs") ?? 250);
                Instance.Topmost.EnableTopmostStyle = Convert.ToBoolean(IsC_TopmostKey?.GetValue("EnableTopmostStyle") ?? true);
                Instance.Topmost.EnableToolWindow = Convert.ToBoolean(IsC_TopmostKey?.GetValue("EnableToolWindow") ?? true);
                Instance.Topmost.EnableNoActivate = Convert.ToBoolean(IsC_TopmostKey?.GetValue("EnableNoActivate") ?? true);
                Instance.Topmost.EnableForegroundHook = Convert.ToBoolean(IsC_TopmostKey?.GetValue("EnableForegroundHook") ?? true);
                Instance.Topmost.EnableUiaDetection = Convert.ToBoolean(IsC_TopmostKey?.GetValue("EnableUiaDetection") ?? true);
                var extraKeywordsJson = IsC_TopmostKey?.GetValue("ExtraTitleKeywords") as string;
                Instance.Topmost.ExtraTitleKeywords = JsonSerializer.Deserialize<List<string>>(extraKeywordsJson ?? "[]") ?? ["FluentShower", "LiquidShower"];
                Instance.Hotkey.Enabled = Convert.ToBoolean(IsC_HotkeyKey?.GetValue("Enabled") ?? false);
                Instance.Hotkey.Modifiers = (HotkeyModifiers)Convert.ToInt32(
                    IsC_HotkeyKey?.GetValue("Modifiers") ?? (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt));
                Instance.Hotkey.Key = IsC_HotkeyKey?.GetValue("Key") as string ?? "C";
                Instance.Hotkey.Scope = (HotkeyScope)Math.Clamp(
                    Convert.ToInt32(IsC_HotkeyKey?.GetValue("Scope") ?? (int)HotkeyScope.Global), 0, 1);
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
            RegistryKey IsC_TTSKey = IsC_RootKey?.OpenSubKey("TTS", writable: true) ?? IsC_RootKey?.CreateSubKey("TTS", writable: true);
            RegistryKey IsC_CallKey = IsC_RootKey?.OpenSubKey("Call", writable: true) ?? IsC_RootKey?.CreateSubKey("Call", writable: true);
            RegistryKey IsC_SecurityKey = IsC_RootKey?.OpenSubKey("Security", writable: true) ?? IsC_RootKey?.CreateSubKey("Security", writable: true);
            RegistryKey IsC_AlgorithmKey = IsC_RootKey?.OpenSubKey("Algorithm", writable: true) ?? IsC_RootKey?.CreateSubKey("Algorithm", writable: true);
            RegistryKey IsC_AppearanceKey = IsC_RootKey?.OpenSubKey("Appearance", writable: true) ?? IsC_RootKey?.CreateSubKey("Appearance", writable: true);
            RegistryKey IsC_TopmostKey = IsC_RootKey?.OpenSubKey("Topmost", writable: true) ?? IsC_RootKey?.CreateSubKey("Topmost", writable: true);
            RegistryKey IsC_HotkeyKey = IsC_RootKey?.OpenSubKey("Hotkey", writable: true) ?? IsC_RootKey?.CreateSubKey("Hotkey", writable: true);

            IsC_GeneralKey?.SetValue("BreakDisable", Instance.General.BreakDisable);
            IsC_GeneralKey?.SetValue("Interruptable", Instance.General.Interruptable);
            IsC_ProfileKey?.SetValue("ProfileNum", Instance.Profile.ProfileNum);
            IsC_ProfileKey?.SetValue("DefaultProfileName", Instance.Profile.DefaultProfile.ToString());
            IsC_ProfileKey?.SetValue("IsPreferProfile", Instance.Profile.IsPreferProfile);
            IsC_ProfileKey?.SetValue("ProfileList", JsonSerializer.Serialize(Instance.Profile.ProfileList));
            IsC_ProfileKey?.SetValue("PreferProfile", JsonSerializer.Serialize(Instance.Profile.ProfilePrefer));
            IsC_HoverKey?.SetValue("IsEnable", Instance.Hover.IsEnable);
            IsC_HoverKey?.SetValue("ScalingFactor", Instance.Hover.ScalingFactor);
            IsC_HoverKey?.SetValue("HoverLayout", Instance.Hover.HoverLayout);
            IsC_HoverKey?.SetValue("LayoutDirection", Instance.Hover.LayoutDirection);
            IsC_HoverKey?.SetValue("CallButtonWidth", Instance.Hover.CallButtonWidth);
            IsC_HoverKey?.SetValue("SnapToScreenEdge", Instance.Hover.SnapToScreenEdge);
            IsC_HoverKey?.SetValue("HoverTheme", Instance.Hover.HoverTheme);
            IsC_HoverKey_Position?.SetValue("X", Instance.Hover.Position.X);
            IsC_HoverKey_Position?.SetValue("Y", Instance.Hover.Position.Y);
            IsC_TTSKey?.SetValue("BeforeText", Instance.TTS.BeforeText);
            IsC_TTSKey?.SetValue("AfterText", Instance.TTS.AfterText);
            IsC_TTSKey?.SetValue("Provider", Instance.TTS.Provider.ToString());
            IsC_CallKey?.SetValue("NotifyMethod", Instance.Call.NotifyMethod);
            IsC_CallKey?.SetValue("ShowerTheme", Instance.Call.ShowerTheme);
            IsC_CallKey?.SetValue("BaseTime", Instance.Call.BaseTime);
            IsC_CallKey?.SetValue("AdditionalTime", Instance.Call.AdditionalTime);
            IsC_CallKey?.SetValue("MaxDrawCount", Instance.Call.MaxDrawCount);
            IsC_SecurityKey?.SetValue("ViewPasswordHash", Instance.Security.ViewPasswordHash);
            IsC_SecurityKey?.SetValue("IsViewPasswordEnabled", Instance.Security.IsViewPasswordEnabled);
            IsC_SecurityKey?.SetValue("EditPasswordHash", Instance.Security.EditPasswordHash);
            IsC_SecurityKey?.SetValue("IsEditPasswordEnabled", Instance.Security.IsEditPasswordEnabled);
            IsC_AlgorithmKey?.SetValue("HalfRecoveryDistance", Instance.Algorithm.HalfRecoveryDistance);
            IsC_AlgorithmKey?.SetValue("CurvePower", Instance.Algorithm.CurvePower);
            IsC_AlgorithmKey?.SetValue("Gamma", Instance.Algorithm.Gamma);
            IsC_AlgorithmKey?.SetValue("RMin", Instance.Algorithm.RMin);
            IsC_AlgorithmKey?.SetValue("RMax", Instance.Algorithm.RMax);
            IsC_AppearanceKey?.SetValue("AccentColor", Instance.Appearance.AccentColor);
            IsC_AppearanceKey?.SetValue("ResultTextColor", Instance.Appearance.ResultTextColor);
            IsC_AppearanceKey?.SetValue("HoverText", Instance.Appearance.HoverText);
            IsC_AppearanceKey?.SetValue("HoverImagePath", Instance.Appearance.HoverImagePath);
            IsC_AppearanceKey?.SetValue("ResultImagePath", Instance.Appearance.ResultImagePath);
            IsC_AppearanceKey?.SetValue("FontFamily", Instance.Appearance.FontFamily);
            IsC_AppearanceKey?.SetValue("ResultFontSize", Instance.Appearance.ResultFontSize);
            IsC_AppearanceKey?.SetValue("ResultBackground", Instance.Appearance.ResultBackground);
            IsC_AppearanceKey?.SetValue("CornerRadius", Instance.Appearance.CornerRadius);
            IsC_AppearanceKey?.SetValue("KeepHoverColor", Instance.Appearance.KeepHoverColor);
            IsC_TopmostKey?.SetValue("Enabled", Instance.Topmost.Enabled);
            IsC_TopmostKey?.SetValue("IntervalMs", Instance.Topmost.IntervalMs);
            IsC_TopmostKey?.SetValue("EnableTopmostStyle", Instance.Topmost.EnableTopmostStyle);
            IsC_TopmostKey?.SetValue("EnableToolWindow", Instance.Topmost.EnableToolWindow);
            IsC_TopmostKey?.SetValue("EnableNoActivate", Instance.Topmost.EnableNoActivate);
            IsC_TopmostKey?.SetValue("EnableForegroundHook", Instance.Topmost.EnableForegroundHook);
            IsC_TopmostKey?.SetValue("EnableUiaDetection", Instance.Topmost.EnableUiaDetection);
            IsC_TopmostKey?.SetValue("ExtraTitleKeywords", JsonSerializer.Serialize(Instance.Topmost.ExtraTitleKeywords));
            IsC_HotkeyKey?.SetValue("Enabled", Instance.Hotkey.Enabled);
            IsC_HotkeyKey?.SetValue("Modifiers", (int)Instance.Hotkey.Modifiers);
            IsC_HotkeyKey?.SetValue("Key", Instance.Hotkey.Key);
            IsC_HotkeyKey?.SetValue("Scope", (int)Instance.Hotkey.Scope);
        }

        /// <summary>
        /// 替换当前设置模型，并将其绑定到注册表保存逻辑。
        /// </summary>
        public void ReplaceModel(SettingsModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            Instance = model;
            SettingsBinder.Bind(Instance, Save);
            Save();
        }

        private static TtsProvider ReadTtsProvider(object? value)
        {
            if (value is string name && Enum.TryParse(name, ignoreCase: true, out TtsProvider provider) &&
                Enum.IsDefined(provider))
            {
                return provider;
            }

            if (value is int numericValue && Enum.IsDefined(typeof(TtsProvider), numericValue))
            {
                return (TtsProvider)numericValue;
            }

            return TtsProvider.None;
        }

        /// <summary>计算密码的 SHA256 哈希（十六进制大写）。</summary>
        public static string HashPassword(string password)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        /// <summary>验证明文密码是否与存储的哈希匹配。</summary>
        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            return string.Equals(HashPassword(password), hash, StringComparison.OrdinalIgnoreCase);
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

            // TTS
            model.TTS.PropertyChanged += (_, _) => onChange();

            // Call
            model.Call.PropertyChanged += (_, _) => onChange();

            // Profile
            model.Profile.PropertyChanged += (_, _) => onChange();

            // Security
            model.Security.PropertyChanged += (_, _) => onChange();

            // Algorithm
            model.Algorithm.PropertyChanged += (_, _) => onChange();

            // Appearance
            model.Appearance.PropertyChanged += (_, _) => onChange();

            // Topmost
            model.Topmost.PropertyChanged += (_, _) => onChange();

            // Hotkey
            model.Hotkey.PropertyChanged += (_, _) => onChange();
        }
    }

}
