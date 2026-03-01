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
            using var reader = new BinaryReader(new MemoryStream(buffer, offset, length));
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
