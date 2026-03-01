using Terraria;
using TerrariaApi.Server;

namespace SkipVersionCheck;

internal static class ItemDropHandler
{
    public static void HandleIncoming(GetDataEventArgs args, int serverMaxItemId)
    {
        int who = args.Msg.whoAmI;
        if (!ClientVersionStore.IsCrossVersion(who, Main.curRelease))
            return;

        if (args.Length < 22)
            return;

        int offset = args.Index;
        int typeOffset = offset + 20;
        short itemType = BitConverter.ToInt16(args.Msg.readBuffer, typeOffset);

        if (itemType >= serverMaxItemId)
        {
            args.Msg.readBuffer[typeOffset] = 0;
            args.Msg.readBuffer[typeOffset + 1] = 0;
        }
    }
}
