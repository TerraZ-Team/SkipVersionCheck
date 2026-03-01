using Terraria.ID;

namespace SkipVersionCheck;

internal static class SkipVersionState
{
    public static int ServerMaxItemId { get; set; } = ItemID.Count;
}
