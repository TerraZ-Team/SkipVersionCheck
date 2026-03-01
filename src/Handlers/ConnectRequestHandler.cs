using System.IO;

using Terraria;
using TerrariaApi.Server;

using TShockAPI;

namespace SkipVersionCheck;

internal sealed class ConnectRequestHandler
{
    private readonly ClientVersionStore _versions;

    public ConnectRequestHandler(ClientVersionStore versions)
    {
        _versions = versions;
    }

    public void Handle(GetDataEventArgs args, PluginConfig config)
    {
        int playerIndex = args.Msg.whoAmI;
        if (playerIndex < 0 || playerIndex >= Main.maxPlayers + 1)
            return;

        _versions.Clear(playerIndex);

        if (!TryReadClientRelease(args, out string clientVersion, out int clientRelease))
            return;

        if (clientRelease < config.MinSupportedRelease)
        {
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] Client (index {playerIndex}) version " +
                    $"{clientVersion} (release {clientRelease}) below minimum {config.MinSupportedRelease}.");
            }
            return;
        }

        _versions.SetVersion(playerIndex, clientRelease);

        if (clientRelease == Main.curRelease)
        {
            _versions.MarkSameAsServer(playerIndex);
            return;
        }

        if (config.DebugLogging)
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] Cross-version client (index {playerIndex}) " +
                $"{clientVersion} ({VersionCatalog.GetLabel(clientRelease)}) connecting to server {Main.curRelease}.");
        }

        if (TryRewriteConnectRequest(args))
        {
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] DEBUG Rewrote ConnectRequest for client {playerIndex}.");
            }
            return;
        }

        _versions.SetLegacyFallback(playerIndex, true);
        HandleLegacyConnectBypass(args, playerIndex, config);
    }

    private static bool TryReadClientRelease(GetDataEventArgs args, out string clientVersion, out int clientRelease)
    {
        clientVersion = string.Empty;
        clientRelease = 0;

        if (args.Length <= 0 ||
            args.Index < 0 ||
            args.Index + args.Length > args.Msg.readBuffer.Length)
        {
            return false;
        }

        try
        {
            using var reader = new BinaryReader(
                new MemoryStream(args.Msg.readBuffer, args.Index, args.Length));
            clientVersion = reader.ReadString();
        }
        catch
        {
            return false;
        }

        if (!clientVersion.StartsWith("Terraria", StringComparison.Ordinal))
            return false;

        return int.TryParse(clientVersion.AsSpan(8), out clientRelease);
    }

    private static bool TryRewriteConnectRequest(GetDataEventArgs args)
    {
        int packetStart = args.Index - 3; // [2 bytes length][1 byte type]
        if (packetStart < 0)
            return false;

        byte[] packet = new PacketFactory()
            .SetType((short)PacketTypes.ConnectRequest)
            .PackString($"Terraria{Main.curRelease}")
            .GetByteData();

        if (packetStart + packet.Length > args.Msg.readBuffer.Length)
            return false;

        Buffer.BlockCopy(packet, 0, args.Msg.readBuffer, packetStart, packet.Length);
        // Length includes packet type + payload (without short length prefix).
        args.Length = packet.Length - 2;
        return true;
    }

    private static void HandleLegacyConnectBypass(GetDataEventArgs args, int playerIndex, PluginConfig config)
    {
        Netplay.Clients[playerIndex].State = 1;
        NetMessage.SendData((int)PacketTypes.ContinueConnecting, playerIndex, -1, null, playerIndex);

        if (config.DebugLogging)
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] DEBUG Legacy ContinueConnecting(3) sent to client {playerIndex}, " +
                $"state={Netplay.Clients[playerIndex].State}");
        }

        args.Handled = true;
    }
}
