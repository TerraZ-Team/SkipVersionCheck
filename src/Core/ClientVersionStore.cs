using Terraria;

namespace SkipVersionCheck;

internal sealed class ClientVersionStore
{
    // -1 = same as server, 0 = empty slot / unknown.
    private readonly int[] _versions = new int[Main.maxPlayers + 1];
    private readonly bool[] _legacyFallback = new bool[Main.maxPlayers + 1];

    public int GetVersion(int playerIndex)
    {
        return IsValidPlayerIndex(playerIndex)
            ? _versions[playerIndex]
            : 0;
    }

    public void SetVersion(int playerIndex, int release)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return;

        _versions[playerIndex] = release;
    }

    public void MarkSameAsServer(int playerIndex)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return;

        _versions[playerIndex] = -1;
    }

    public void SetLegacyFallback(int playerIndex, bool enabled)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return;

        _legacyFallback[playerIndex] = enabled;
    }

    public bool UsesLegacyFallback(int playerIndex)
    {
        return IsValidPlayerIndex(playerIndex) && _legacyFallback[playerIndex];
    }

    public bool IsCrossVersion(int playerIndex, int serverRelease)
    {
        int release = GetVersion(playerIndex);
        return release > 0 && release != serverRelease;
    }

    public bool NeedsSpawnTranslation(int playerIndex)
    {
        int release = GetVersion(playerIndex);
        return release > 0 && release < VersionCatalog.SpawnPacketV2Release;
    }

    public int Clear(int playerIndex)
    {
        if (!IsValidPlayerIndex(playerIndex))
            return 0;

        int previousVersion = _versions[playerIndex];
        _versions[playerIndex] = 0;
        _legacyFallback[playerIndex] = false;
        return previousVersion;
    }

    private static bool IsValidPlayerIndex(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < Main.maxPlayers + 1;
    }
}
