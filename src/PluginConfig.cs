using TShockAPI;
using TShockAPI.Configuration;

namespace SkipVersionCheck;

/// <summary>
/// Configuration file for SkipVersionCheck plugin.
/// Saved to tshock/SkipVersionCheck.json.
/// </summary>
public class PluginConfig
{
    /// <summary>
    /// Enable verbose debug logging for packet-level diagnostics.
    /// Default: false.
    /// </summary>
    public bool DebugLogging { get; set; } = false;

    /// <summary>
    /// Minimum supported client release number.
    /// Clients below this version will be rejected.
    /// Default: 269 (v1.4.4.0).
    /// </summary>
    public int MinSupportedRelease { get; set; } = 269;

    /// <summary>
    /// Enables Journey-mode compatibility adjustments for cross-version clients.
    /// When false, the plugin will not force/clear the Journey bit in PlayerInfo.
    /// Default: true.
    /// </summary>
    public bool SupportJourneyClients { get; set; } = true;

    private static string ConfigPath => Path.Combine(TShock.SavePath, "SkipVersionCheck.json");
    private static readonly ConfigFile<PluginConfig> ConfigFile = new();

    public static PluginConfig Load()
    {
        try
        {
            ConfigFile.Read(ConfigPath, out bool incomplete);
            PluginConfig settings = ConfigFile.Settings ?? new PluginConfig();

            // Defensive bounds for corrupted/manual edits.
            if (settings.MinSupportedRelease < 1)
                settings.MinSupportedRelease = 269;

            if (incomplete)
            {
                ConfigFile.Settings = settings;
                ConfigFile.Write(ConfigPath);
            }

            return settings;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError(
                $"[SkipVersionCheck] Error loading config: {ex.Message}. Using defaults.");

            PluginConfig defaults = new();
            try
            {
                ConfigFile.Settings = defaults;
                ConfigFile.Write(ConfigPath);
            }
            catch (Exception writeEx)
            {
                TShock.Log.ConsoleError(
                    $"[SkipVersionCheck] Error writing default config: {writeEx.Message}");
            }

            return defaults;
        }
    }

    public void Save()
    {
        try
        {
            ConfigFile.Settings = this;
            ConfigFile.Write(ConfigPath);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError(
                $"[SkipVersionCheck] Error saving config: {ex.Message}");
        }
    }
}
