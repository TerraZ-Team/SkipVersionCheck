using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using Terraria.Net;

namespace SkipVersionCheck;

/// <summary>
/// Hooks into Terraria's NetManager to filter outgoing NetModule packets
/// sent to cross-version clients. This prevents the server from sending
/// items/data that the client's version doesn't understand.
/// Ported from the Crossplay plugin by Moneylover3246.
/// </summary>
internal static class NetModuleHandler
{
    internal static void OnBroadcast(
        On.Terraria.Net.NetManager.orig_Broadcast_NetPacket_int orig,
        NetManager self,
        NetPacket packet,
        int ignoreClient)
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (i != ignoreClient && Netplay.Clients[i].IsConnected() && !IsInvalidNetPacket(packet, i))
                self.SendData(Netplay.Clients[i].Socket, packet);
        }
    }

    internal static void OnSendToClient(
        On.Terraria.Net.NetManager.orig_SendToClient orig,
        NetManager self,
        NetPacket packet,
        int playerId)
    {
        if (!IsInvalidNetPacket(packet, playerId))
            orig(self, packet, playerId);
    }

    private static bool IsInvalidNetPacket(NetPacket packet, int playerId)
    {
        int clientVersion = ClientVersionStore.GetVersion(playerId);
        if (clientVersion <= 0)
            return false;

        if (packet.Id != 5) // CreativeUnlocksPlayerReport
            return false;

        byte[]? data = packet.Buffer?.Data;
        if (data == null || data.Length < 5)
            return false;

        short itemNetId = Unsafe.As<byte, short>(ref data[3]);
        int maxItems = VersionCatalog.GetMaxItemsForVersion(clientVersion, ItemID.Count);
        return itemNetId > maxItems;
    }
}
