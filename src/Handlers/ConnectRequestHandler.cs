using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using SkipVersionCheck.Configuration;

namespace SkipVersionCheck;

internal static class ConnectRequestHandler
{
    public static void Handle(GetDataEventArgs args, ConfigSettings config)
    {
        int playerIndex = args.Msg.whoAmI;
        if (playerIndex < 0 || playerIndex >= Main.maxPlayers + 1)
            return;

        ClientVersionStore.Clear(playerIndex);

        if (!TryReadClientRelease(args, out string clientVersion, out int clientRelease))
            return;

        if (config.DebugLogging)
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] Client {playerIndex} version: {clientVersion} (release {clientRelease}), server release: {Main.curRelease}");
        }

        if (clientRelease < config.MinSupportedRelease)
            return;

        ClientVersionStore.SetVersion(playerIndex, clientRelease);

        if (clientRelease == Main.curRelease)
        {
            ClientVersionStore.MarkSameAsServer(playerIndex);
        }
        else
        {
            ClientVersionStore.SetLegacyFallback(playerIndex, true);
        }

        HandleLegacyConnectBypass(args, playerIndex);
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

        if (TryReadVersionString(args.Msg.readBuffer, args.Index, args.Length, out clientVersion) &&
            TryExtractRelease(clientVersion, out clientRelease))
        {
            return true;
        }

        // Some server builds expose ConnectRequest with the message type byte still
        // counted in args.Length; retry by skipping a potential type byte.
        if (args.Length > 1 &&
            TryReadVersionString(args.Msg.readBuffer, args.Index + 1, args.Length - 1, out clientVersion) &&
            TryExtractRelease(clientVersion, out clientRelease))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadVersionString(byte[] buffer, int offset, int length, out string version)
    {
        version = string.Empty;
        if (offset < 0 || length <= 0 || offset + length > buffer.Length)
            return false;

        try
        {
            using BinaryReader reader = new BinaryReader(new MemoryStream(buffer, offset, length));
            version = reader.ReadString();
            return !string.IsNullOrWhiteSpace(version);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractRelease(string clientVersion, out int clientRelease)
    {
        clientRelease = 0;
        if (!clientVersion.StartsWith("Terraria", StringComparison.Ordinal))
            return false;

        ReadOnlySpan<char> span = clientVersion.AsSpan("Terraria".Length);
        int digitCount = 0;
        while (digitCount < span.Length && char.IsDigit(span[digitCount]))
            digitCount++;

        if (digitCount == 0)
            return false;

        return int.TryParse(span.Slice(0, digitCount), out clientRelease);
    }

    private static void HandleLegacyConnectBypass(GetDataEventArgs args, int playerIndex)
    {
        Netplay.Clients[playerIndex].State = (int)ConnectionState.AssigningPlayerSlot;
        NetMessage.SendData((int)PacketTypes.ContinueConnecting, playerIndex, -1, null, playerIndex);
        args.Handled = true;
    }
}
