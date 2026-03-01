namespace SkipVersionCheck;

internal static class VersionCatalog
{
    public const int SpawnPacketV2Release = 316;
    public const int PlayerInfoVoiceV2Release = 318;
    public const byte DifficultyExtraAccessoryFlag = 4;
    public const byte DifficultyCreativeFlag = 8;

    private static readonly IReadOnlyDictionary<int, string> KnownVersions = new Dictionary<int, string>
    {
        { 315, "v1.4.5.0" },
        { 316, "v1.4.5.3" },
        { 317, "v1.4.5.5" },
        { 318, "v1.4.5.5" },
    };

    private static readonly IReadOnlyDictionary<int, int> MaxItems = new Dictionary<int, int>
    {
        { 315, 6145 },
        { 316, 6145 },
        { 317, 6145 },
        { 318, 6145 },
    };

    public static IEnumerable<string> GetKnownVersionNames() => KnownVersions.Values;

    public static string GetLabel(int release)
    {
        return KnownVersions.TryGetValue(release, out string? label)
            ? label
            : $"release {release}";
    }

    public static int GetMaxItemsForVersion(int release, int serverMaxItemId)
    {
        return MaxItems.TryGetValue(release, out int max)
            ? max
            : serverMaxItemId;
    }
}
