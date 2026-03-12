namespace SkipVersionCheck;

[Flags]
internal enum ClientFeatures
{
    None = 0,
    SpawnPacketV2 = 1 << 0,
    PlayerInfoVoiceV2 = 1 << 1,
}

internal readonly record struct ReleaseInfo(
    int Release,
    string Label,
    ClientFeatures Features,
    int MaxItemId)
{
    public bool Supports(ClientFeatures features)
    {
        return (Features & features) == features;
    }
}

internal static class VersionCatalog
{
    private const int KnownClientMaxItemId = 6145;

    private static readonly ReleaseInfo[] KnownReleases =
    [
        new(315, "v1.4.5.0", ClientFeatures.None, KnownClientMaxItemId),
        new(316, "v1.4.5.3", ClientFeatures.SpawnPacketV2, KnownClientMaxItemId),
        new(317, "v1.4.5.5", ClientFeatures.SpawnPacketV2, KnownClientMaxItemId),
        new(318, "v1.4.5.5", ClientFeatures.SpawnPacketV2 | ClientFeatures.PlayerInfoVoiceV2, KnownClientMaxItemId),
        new(319, "v1.4.5.6", ClientFeatures.SpawnPacketV2 | ClientFeatures.PlayerInfoVoiceV2, KnownClientMaxItemId),
    ];

    private static readonly IReadOnlyDictionary<int, ReleaseInfo> KnownReleaseLookup =
        KnownReleases.ToDictionary(info => info.Release);

    public static IEnumerable<string> GetKnownVersionNames()
    {
        return KnownReleases
            .Select(info => info.Label)
            .Distinct(StringComparer.Ordinal);
    }

    public static string GetLabel(int release)
    {
        return KnownReleaseLookup.TryGetValue(release, out ReleaseInfo info)
            ? info.Label
            : $"release {release}";
    }

    public static int GetMaxItemsForVersion(int release, int serverMaxItemId)
    {
        return KnownReleaseLookup.TryGetValue(release, out ReleaseInfo info)
            ? info.MaxItemId
            : serverMaxItemId;
    }

    public static bool Supports(int release, ClientFeatures features)
    {
        if (release <= 0)
            return false;

        if (KnownReleaseLookup.TryGetValue(release, out ReleaseInfo exact))
            return exact.Supports(features);

        // Unknown future releases inherit feature support from the nearest lower
        // known release. Payload sizes like MaxItemId stay explicit per release.
        for (int i = KnownReleases.Length - 1; i >= 0; i--)
        {
            ReleaseInfo candidate = KnownReleases[i];
            if (candidate.Release <= release)
                return candidate.Supports(features);
        }

        return false;
    }
}
