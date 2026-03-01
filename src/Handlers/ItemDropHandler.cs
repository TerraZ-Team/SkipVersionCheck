using Terraria;
using TerrariaApi.Server;

using TShockAPI;

namespace SkipVersionCheck;

internal sealed class ItemDropHandler
{
    private readonly ClientVersionStore _versions;

    public ItemDropHandler(ClientVersionStore versions)
    {
        _versions = versions;
    }

    public void HandleIncoming(GetDataEventArgs args, int serverMaxItemId, PluginConfig config)
    {
        int who = args.Msg.whoAmI;
        if (!_versions.IsCrossVersion(who, Main.curRelease))
            return;

        if (args.Length < 22)
            return;

        int offset = args.Index;
        int typeOffset = offset + 20;
        short itemType = BitConverter.ToInt16(args.Msg.readBuffer, typeOffset);

        if (itemType >= serverMaxItemId)
        {
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] DEBUG Filtered unsupported item {itemType} from client {who}");
            }

            args.Msg.readBuffer[typeOffset] = 0;
            args.Msg.readBuffer[typeOffset + 1] = 0;
        }
    }
}
