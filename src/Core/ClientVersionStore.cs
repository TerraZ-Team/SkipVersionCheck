using Terraria;

namespace SkipVersionCheck;

internal static class ClientVersionStore
{
    // -1 = same as server, 0 = empty slot / unknown.
    private static readonly int[] Versions = new int[Main.maxPlayers + 1];
    private static readonly bool[] LegacyFallback = new bool[Main.maxPlayers + 1];

    public static int GetVersion(int playerIndex)
    {
        return IsValidPlayerIndex(playerIndex)
            ? Versions[playerIndex]
            : 0;
    }

    public static void SetVersion(int playerIndex, int release)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return;

        Versions[playerIndex] = release;
    }

    public static void MarkSameAsServer(int playerIndex)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return;

        Versions[playerIndex] = -1;
    }

    public static void SetLegacyFallback(int playerIndex, bool enabled)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return;

        LegacyFallback[playerIndex] = enabled;
    }

    public static bool UsesLegacyFallback(int playerIndex)
    {
        return IsValidPlayerIndex(playerIndex) && LegacyFallback[playerIndex];
    }

    public static bool IsCrossVersion(int playerIndex, int serverRelease)
    {
        int release = GetVersion(playerIndex);
        return release > 0 && release != serverRelease;
    }

    public static bool NeedsSpawnTranslation(int playerIndex)
    {
        int release = GetVersion(playerIndex);
        return release > 0 && !VersionCatalog.Supports(release, ClientFeatures.SpawnPacketV2);
    }

    public static int Clear(int playerIndex)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return 0;

        int previousVersion = Versions[playerIndex];
        Versions[playerIndex] = 0;
        LegacyFallback[playerIndex] = false;
        return previousVersion;
    }

    private static bool IsValidPlayerIndex(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < Main.maxPlayers + 1;
    }
}
