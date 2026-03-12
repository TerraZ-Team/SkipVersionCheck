using TShockAPI;
using TShockAPI.Configuration;

namespace SkipVersionCheck.Configuration;

internal static class Config
{
    private static string ConfigPath => Path.Combine(TShock.SavePath ?? "tshock", "SkipVersionCheck.json");
    private static readonly ConfigFile<ConfigSettings> ConfigFile = new();

    public static ConfigSettings Settings => ConfigFile.Settings;

    static Config()
    {
        Reload();
    }

    public static void Reload()
    {
        try
        {
            ConfigFile.Read(ConfigPath, out bool incomplete);

            ConfigSettings settings = ConfigFile.Settings ?? new ConfigSettings();
            bool changed = false;

            if (settings.MinSupportedRelease < 1)
            {
                settings.MinSupportedRelease = 269;
                changed = true;
            }

            if (incomplete || changed || ConfigFile.Settings == null)
            {
                ConfigFile.Settings = settings;
                ConfigFile.Write(ConfigPath);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[SkipVersionCheck] Failed to load config: {ex.Message}");
            ConfigFile.Settings = new ConfigSettings();
            ConfigFile.Write(ConfigPath);
        }
    }
}
