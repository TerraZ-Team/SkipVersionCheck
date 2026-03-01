using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;

using TShockAPI;

namespace SkipVersionCheck;

internal sealed class SpawnPacketHandler
{
    private readonly ClientVersionStore _versions;

    public SpawnPacketHandler(ClientVersionStore versions)
    {
        _versions = versions;
    }

    public void HandleOutgoing(SendDataEventArgs args, PluginConfig config)
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
            if (_versions.NeedsSpawnTranslation(remoteClient))
            {
                SendRawPacket(remoteClient, oldPacket);
                args.Handled = true;
                anyCrossVersion = true;
            }
        }
        else
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (i == ignoreClient || !Netplay.Clients[i].IsConnected())
                    continue;

                if (_versions.NeedsSpawnTranslation(i))
                {
                    SendRawPacket(i, oldPacket);
                    anyCrossVersion = true;
                }
            }

            if (anyCrossVersion)
            {
                bool allTargetsAreCrossVersion = true;
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (i == ignoreClient || !Netplay.Clients[i].IsConnected())
                        continue;

                    if (!_versions.NeedsSpawnTranslation(i))
                    {
                        allTargetsAreCrossVersion = false;
                        break;
                    }
                }

                if (allTargetsAreCrossVersion)
                    args.Handled = true;
            }
        }

        if (anyCrossVersion && config.DebugLogging)
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] Translated outgoing spawn packet for player {playerIndex}, " +
                $"handled={args.Handled}");
        }
    }

    public void HandleIncoming(GetDataEventArgs args, PluginConfig config)
    {
        int who = args.Msg.whoAmI;
        if (!_versions.NeedsSpawnTranslation(who))
            return;

        int payloadLength = args.Length - 1;
        const int oldPayloadLength = 10;
        const int newPayloadLength = 15;

        if (payloadLength >= newPayloadLength)
            return;

        if (payloadLength < oldPayloadLength)
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
            BitConverter.GetBytes((short)0).CopyTo(args.Msg.readBuffer, offset + 9);  // numberOfDeathsPVE
            BitConverter.GetBytes((short)0).CopyTo(args.Msg.readBuffer, offset + 11); // numberOfDeathsPVP
            args.Msg.readBuffer[offset + 13] = team;
            args.Msg.readBuffer[offset + 14] = spawnContext;

            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] Padded incoming spawn packet from client {who}: " +
                    $"spawn=({spawnX},{spawnY}), respawnTimer={respawnTimer}, " +
                    $"team={team}, context={spawnContext}");
            }
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
