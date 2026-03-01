using System.IO;
using System.Text;

using Terraria;
using Terraria.ID;
using TerrariaApi.Server;

using TShockAPI;

namespace SkipVersionCheck;

internal sealed class PlayerInfoHandler
{
    private readonly ClientVersionStore _versions;

    public PlayerInfoHandler(ClientVersionStore versions)
    {
        _versions = versions;
    }

    public void Handle(GetDataEventArgs args, PluginConfig config)
    {
        int who = args.Msg.whoAmI;
        if (!_versions.IsCrossVersion(who, Main.curRelease))
            return;

        NormalizePlayerInfoPacket(args, who, config);

        if (!TryGetDifficultyByteIndex(args, out int flagsIndex))
            return;

        ref byte difficultyFlags = ref args.Msg.readBuffer[flagsIndex];

        if ((difficultyFlags & VersionCatalog.DifficultyExtraAccessoryFlag) != 0 &&
            Main.player[who] != null &&
            !Main.player[who].extraAccessory)
        {
            Main.player[who].extraAccessory = true;
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] Demon Heart extra slot ACTIVATED for client {who}");
            }
        }

        if (!config.SupportJourneyClients)
            return;

        if (Main.GameMode == GameModeID.Creative)
        {
            if ((difficultyFlags & VersionCatalog.DifficultyCreativeFlag) == 0)
            {
                if (config.DebugLogging)
                {
                    TShock.Log.ConsoleInfo(
                        $"[SkipVersionCheck] Enabled journey mode flag for cross-version client {who}");
                }

                difficultyFlags |= VersionCatalog.DifficultyCreativeFlag;
                if (Main.ServerSideCharacter)
                {
                    NetMessage.SendData((int)PacketTypes.PlayerInfo, who, -1, null, who);
                }
            }
            return;
        }

        if ((difficultyFlags & VersionCatalog.DifficultyCreativeFlag) != 0)
        {
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] Disabled journey mode flag for cross-version client {who}");
            }

            difficultyFlags = (byte)(difficultyFlags & ~VersionCatalog.DifficultyCreativeFlag);
        }
    }

    public void HandleLateFallback(GetDataEventArgs args, PluginConfig config)
    {
        if (args.MsgID != PacketTypes.PlayerInfo)
            return;

        int who = args.Msg.whoAmI;
        if (!_versions.IsCrossVersion(who, Main.curRelease) || !_versions.UsesLegacyFallback(who))
            return;

        // Legacy connect bypass needs manual PlayerInfo flow.
        args.Handled = true;

        if (TryGetPlayerName(args, _versions.GetVersion(who), out string name) &&
            !string.IsNullOrWhiteSpace(name) &&
            Main.player[who] != null &&
            string.IsNullOrEmpty(Main.player[who].name))
        {
            Main.player[who].name = name;
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] Fixed blank player name -> '{name}' for client {who}");
            }
        }

        if (TryGetDifficultyByteIndex(args, out int flagsIndex))
        {
            byte difficultyFlags = args.Msg.readBuffer[flagsIndex];
            bool hasExtraAccessory = (difficultyFlags & VersionCatalog.DifficultyExtraAccessoryFlag) != 0;
            if (hasExtraAccessory && Main.player[who] != null && !Main.player[who].extraAccessory)
            {
                Main.player[who].extraAccessory = true;
                if (config.DebugLogging)
                {
                    TShock.Log.ConsoleInfo(
                        $"[SkipVersionCheck] Demon Heart extra slot ACTIVATED for client {who} (legacy fallback)");
                }
            }
        }

        if (config.DebugLogging)
        {
            string playerName = Main.player[who]?.name ?? "(null)";
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] DEBUG OnGetDataLate fallback: client={who}, " +
                $"name='{playerName}', state={Netplay.Clients[who].State}");
        }

        if (Netplay.Clients[who].State == 1)
        {
            Netplay.Clients[who].State = 2;
            NetMessage.SendData((int)PacketTypes.WorldInfo, who);

            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] DEBUG Sent WorldInfo(7) to legacy client {who}, " +
                    $"state now={Netplay.Clients[who].State}");
            }
        }
    }

    private void NormalizePlayerInfoPacket(GetDataEventArgs args, int who, PluginConfig config)
    {
        int clientRelease = _versions.GetVersion(who);
        if (clientRelease <= 0)
            return;

        bool clientHasVoiceFields = clientRelease >= VersionCatalog.PlayerInfoVoiceV2Release;
        bool serverHasVoiceFields = Main.curRelease >= VersionCatalog.PlayerInfoVoiceV2Release;
        if (clientHasVoiceFields == serverHasVoiceFields)
            return;

        int payloadStart = args.Index;
        int payloadLength = args.Length - 1;
        if (payloadLength < 3 || payloadStart < 0)
            return;

        const int insertOffset = 2; // after playerId + skinVariant
        const int voiceFieldsLength = 5; // voiceVariant + voicePitchOffset
        byte[] translatedPayload;

        if (clientHasVoiceFields && !serverHasVoiceFields)
        {
            if (payloadLength <= insertOffset + voiceFieldsLength)
                return;

            translatedPayload = new byte[payloadLength - voiceFieldsLength];
            Buffer.BlockCopy(args.Msg.readBuffer, payloadStart, translatedPayload, 0, insertOffset);
            Buffer.BlockCopy(
                args.Msg.readBuffer,
                payloadStart + insertOffset + voiceFieldsLength,
                translatedPayload,
                insertOffset,
                payloadLength - insertOffset - voiceFieldsLength);
        }
        else
        {
            translatedPayload = new byte[payloadLength + voiceFieldsLength];
            Buffer.BlockCopy(args.Msg.readBuffer, payloadStart, translatedPayload, 0, insertOffset);
            translatedPayload[insertOffset] = 0; // voiceVariant
            Buffer.BlockCopy(
                args.Msg.readBuffer,
                payloadStart + insertOffset,
                translatedPayload,
                insertOffset + voiceFieldsLength,
                payloadLength - insertOffset);
        }

        byte[] translatedPacket = new PacketFactory()
            .SetType((short)PacketTypes.PlayerInfo)
            .PackBuffer(translatedPayload)
            .GetByteData();

        int packetStart = args.Index - 3;
        if (packetStart < 0 || packetStart + translatedPacket.Length > args.Msg.readBuffer.Length)
            return;

        Buffer.BlockCopy(translatedPacket, 0, args.Msg.readBuffer, packetStart, translatedPacket.Length);
        args.Length = translatedPacket.Length - 2;

        if (config.DebugLogging)
        {
            string direction = clientHasVoiceFields ? "newer->older" : "older->newer";
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] Normalized PlayerInfo ({direction}) for client {who}, " +
                $"len {payloadLength} -> {translatedPayload.Length}");
        }
    }

    private bool TryGetDifficultyByteIndex(GetDataEventArgs args, out int difficultyIndex)
    {
        bool serverHasVoiceFields = Main.curRelease >= VersionCatalog.PlayerInfoVoiceV2Release;
        if (TryGetDifficultyByteIndex(args, serverHasVoiceFields, out difficultyIndex))
            return true;

        return TryGetDifficultyByteIndex(args, !serverHasVoiceFields, out difficultyIndex);
    }

    private static bool TryGetDifficultyByteIndex(
        GetDataEventArgs args,
        bool hasVoiceFields,
        out int difficultyIndex)
    {
        difficultyIndex = -1;

        if (args.Length <= 1 || args.Index < 0)
            return false;

        int payloadStart = args.Index;
        int payloadLength = args.Length - 1;
        int payloadEndExclusive = payloadStart + payloadLength;
        if (payloadEndExclusive > args.Msg.readBuffer.Length)
            return false;

        int offset = payloadStart;
        int fixedPrefixSize = hasVoiceFields ? 8 : 3;
        if (offset + fixedPrefixSize > payloadEndExclusive)
            return false;
        offset += fixedPrefixSize;

        if (!TryRead7BitEncodedInt(args.Msg.readBuffer, payloadEndExclusive, ref offset, out int nameByteLength))
            return false;

        if (nameByteLength < 0 || offset + nameByteLength > payloadEndExclusive)
            return false;
        offset += nameByteLength;

        const int postNameSize = 1 + 2 + 1; // hairDye + hideVisualFlags + hideMisc
        if (offset + postNameSize > payloadEndExclusive)
            return false;
        offset += postNameSize;

        const int colorsSize = 7 * 3;
        if (offset + colorsSize >= payloadEndExclusive)
            return false;
        offset += colorsSize;

        difficultyIndex = offset;
        return true;
    }

    private static bool TryGetPlayerName(GetDataEventArgs args, int clientRelease, out string name)
    {
        bool hasVoiceFields = clientRelease >= VersionCatalog.PlayerInfoVoiceV2Release;
        if (TryGetPlayerName(args, hasVoiceFields, out name))
            return true;

        return TryGetPlayerName(args, !hasVoiceFields, out name);
    }

    private static bool TryGetPlayerName(GetDataEventArgs args, bool hasVoiceFields, out string name)
    {
        name = string.Empty;

        if (args.Length <= 1 || args.Index < 0)
            return false;

        int payloadStart = args.Index;
        int payloadLength = args.Length - 1;
        int payloadEndExclusive = payloadStart + payloadLength;
        if (payloadEndExclusive > args.Msg.readBuffer.Length)
            return false;

        int offset = payloadStart;
        int fixedPrefixSize = hasVoiceFields ? 8 : 3;
        if (offset + fixedPrefixSize > payloadEndExclusive)
            return false;
        offset += fixedPrefixSize;

        if (!TryRead7BitEncodedInt(args.Msg.readBuffer, payloadEndExclusive, ref offset, out int nameByteLength))
            return false;

        if (nameByteLength < 0 || offset + nameByteLength > payloadEndExclusive)
            return false;

        name = Encoding.UTF8.GetString(args.Msg.readBuffer, offset, nameByteLength);
        return true;
    }

    private static bool TryRead7BitEncodedInt(
        byte[] buffer,
        int endExclusive,
        ref int offset,
        out int value)
    {
        value = 0;
        int shift = 0;

        for (int i = 0; i < 5; i++)
        {
            if (offset >= endExclusive)
                return false;

            byte current = buffer[offset++];
            value |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return true;

            shift += 7;
        }

        return false;
    }
}
