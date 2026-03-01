using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;

namespace SkipVersionCheck;

internal static class SpawnPacketHandler
{
    public static void HandleOutgoing(SendDataEventArgs args)
    {
        int playerIndex = args.number;
        if (playerIndex < 0 || playerIndex >= Main.maxPlayers)
            return;

        Player player = Main.player[playerIndex];
        if (player == null)
            return;

        byte[] oldPacket = new PacketFactory()
            .SetType((short)PacketTypes.PlayerSpawn)
            .PackByte((byte)playerIndex)
            .PackInt16((short)player.SpawnX)
            .PackInt16((short)player.SpawnY)
            .PackInt32(player.respawnTimer)
            .PackByte((byte)args.number2) // spawnContext
            .GetByteData();

        int remoteClient = args.remoteClient;
        int ignoreClient = args.ignoreClient;
        bool anyCrossVersion = false;

        if (remoteClient >= 0 && remoteClient < 256)
        {
            if (ClientVersionStore.NeedsSpawnTranslation(remoteClient))
            {
                SendRawPacket(remoteClient, oldPacket);
                args.Handled = true;
            }

            return;
        }

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (i == ignoreClient || !Netplay.Clients[i].IsConnected())
                continue;

            if (ClientVersionStore.NeedsSpawnTranslation(i))
            {
                SendRawPacket(i, oldPacket);
                anyCrossVersion = true;
            }
        }

        if (!anyCrossVersion)
            return;

        bool allTargetsAreCrossVersion = true;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (i == ignoreClient || !Netplay.Clients[i].IsConnected())
                continue;

            if (!ClientVersionStore.NeedsSpawnTranslation(i))
            {
                allTargetsAreCrossVersion = false;
                break;
            }
        }

        if (allTargetsAreCrossVersion)
            args.Handled = true;
    }

    public static void HandleIncoming(GetDataEventArgs args)
    {
        int who = args.Msg.whoAmI;
        if (!ClientVersionStore.NeedsSpawnTranslation(who))
            return;

        int payloadLength = args.Length - 1;
        const int oldPayloadLength = 10;
        const int newPayloadLength = 15;

        if (payloadLength >= newPayloadLength || payloadLength < oldPayloadLength)
            return;

        try
        {
            int offset = args.Index;

            byte playerId = args.Msg.readBuffer[offset];
            short spawnX = BitConverter.ToInt16(args.Msg.readBuffer, offset + 1);
            short spawnY = BitConverter.ToInt16(args.Msg.readBuffer, offset + 3);
            int respawnTimer = BitConverter.ToInt32(args.Msg.readBuffer, offset + 5);
            byte spawnContext = args.Msg.readBuffer[offset + 9];

            byte team = 0;
            if (who >= 0 && who < Main.maxPlayers && Main.player[who] != null)
                team = (byte)Main.player[who].team;

            args.Msg.readBuffer[offset] = playerId;
            BitConverter.GetBytes(spawnX).CopyTo(args.Msg.readBuffer, offset + 1);
            BitConverter.GetBytes(spawnY).CopyTo(args.Msg.readBuffer, offset + 3);
            BitConverter.GetBytes(respawnTimer).CopyTo(args.Msg.readBuffer, offset + 5);
            BitConverter.GetBytes((short)0).CopyTo(args.Msg.readBuffer, offset + 9); // numberOfDeathsPVE
            BitConverter.GetBytes((short)0).CopyTo(args.Msg.readBuffer, offset + 11); // numberOfDeathsPVP
            args.Msg.readBuffer[offset + 13] = team;
            args.Msg.readBuffer[offset + 14] = spawnContext;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError(
                $"[SkipVersionCheck] Error translating incoming spawn packet from client {who}: {ex.Message}");
        }
    }

    private static void SendRawPacket(int clientIndex, byte[] data)
    {
        if (clientIndex < 0 || clientIndex >= 256)
            return;

        var client = Netplay.Clients[clientIndex];
        if (client?.Socket == null || !client.IsConnected())
            return;

        try
        {
            client.Socket.AsyncSend(
                data, 0, data.Length,
                new SocketSendCallback(client.ServerWriteCallBack));
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError(
                $"[SkipVersionCheck] Error sending raw packet to client {clientIndex}: {ex.Message}");
        }
    }
}
