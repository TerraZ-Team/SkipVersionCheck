using System.IO;

using Microsoft.Xna.Framework;

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

    public void HandleLate(GetDataEventArgs args, PluginConfig config)
    {
        if (args.MsgID != PacketTypes.PlayerInfo)
            return;

        int who = args.Msg.whoAmI;
        if (!_versions.IsCrossVersion(who, Main.curRelease))
            return;

        // Original plugin strategy: suppress vanilla PlayerInfo path for all
        // cross-version clients and apply parsed values manually.
        args.Handled = true;

        int clientRelease = _versions.GetVersion(who);
        if (!TryParsePlayerInfo(args, clientRelease, out ParsedPlayerInfo info))
        {
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] DEBUG Failed to parse PlayerInfo for client {who} " +
                    $"(release {clientRelease}).");
            }

            AdvanceHandshakeIfNeeded(who, config);
            return;
        }

        ApplyParsedPlayerInfo(who, info, config);
        AdvanceHandshakeIfNeeded(who, config);
    }

    private void ApplyParsedPlayerInfo(int who, ParsedPlayerInfo info, PluginConfig config)
    {
        Player? player = Main.player[who];
        if (player == null)
            return;

        if (!string.IsNullOrWhiteSpace(info.Name))
            player.name = info.Name;

        player.skinVariant = info.SkinVariant;
        player.hair = info.Hair;
        player.hairDye = info.HairDye;

        // Colors in PlayerInfo packet:
        // hair, skin, eye, shirt, underShirt, pants, shoe.
        player.hairColor = info.HairColor;
        player.skinColor = info.SkinColor;
        player.eyeColor = info.EyeColor;
        player.shirtColor = info.ShirtColor;
        player.underShirtColor = info.UnderShirtColor;
        player.pantsColor = info.PantsColor;
        player.shoeColor = info.ShoeColor;

        ApplyHideVisualFlags(player, info.HideVisualFlags);
        player.hideMisc = (BitsByte)info.HideMisc;

        byte difficultyFlags = NormalizeJourneyFlag(info.DifficultyFlags, who, config);
        player.difficulty = (byte)(difficultyFlags & 0b11);

        bool hasExtraAccessory = (difficultyFlags & VersionCatalog.DifficultyExtraAccessoryFlag) != 0;
        if (hasExtraAccessory && !player.extraAccessory)
        {
            player.extraAccessory = true;
            if (config.DebugLogging)
            {
                TShock.Log.ConsoleInfo(
                    $"[SkipVersionCheck] Demon Heart extra slot ACTIVATED for client {who}");
            }
        }

        if (config.DebugLogging)
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] DEBUG Applied PlayerInfo for client {who}: " +
                $"name='{player.name}', skin={player.skinVariant}, hair={player.hair}, " +
                $"difficulty=0x{difficultyFlags:X2}");
        }
    }

    private static void ApplyHideVisualFlags(Player player, ushort hideVisualFlags)
    {
        bool[] hide = player.hideVisibleAccessory;
        if (hide == null || hide.Length == 0)
            return;

        int count = Math.Min(hide.Length, 16);
        for (int i = 0; i < count; i++)
        {
            hide[i] = (hideVisualFlags & (1 << i)) != 0;
        }
    }

    private static byte NormalizeJourneyFlag(byte difficultyFlags, int who, PluginConfig config)
    {
        if (!config.SupportJourneyClients)
            return difficultyFlags;

        if (Main.GameMode == GameModeID.Creative)
        {
            if ((difficultyFlags & VersionCatalog.DifficultyCreativeFlag) == 0)
            {
                difficultyFlags |= VersionCatalog.DifficultyCreativeFlag;
                if (Main.ServerSideCharacter)
                {
                    NetMessage.SendData((int)PacketTypes.PlayerInfo, who, -1, null, who);
                }
            }

            return difficultyFlags;
        }

        if ((difficultyFlags & VersionCatalog.DifficultyCreativeFlag) != 0)
        {
            difficultyFlags = (byte)(difficultyFlags & ~VersionCatalog.DifficultyCreativeFlag);
        }

        return difficultyFlags;
    }

    private static void AdvanceHandshakeIfNeeded(int who, PluginConfig config)
    {
        if (who < 0 || who >= 256)
            return;

        if (Netplay.Clients[who].State != 1)
            return;

        Netplay.Clients[who].State = 2;
        NetMessage.SendData((int)PacketTypes.WorldInfo, who);

        if (config.DebugLogging)
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] DEBUG Sent WorldInfo(7) to client {who}, " +
                $"state now={Netplay.Clients[who].State}");
        }
    }

    private static bool TryParsePlayerInfo(
        GetDataEventArgs args,
        int clientRelease,
        out ParsedPlayerInfo info)
    {
        bool hasVoiceFields = clientRelease >= VersionCatalog.PlayerInfoVoiceV2Release;
        if (TryParsePlayerInfo(args, hasVoiceFields, out info))
            return true;

        return TryParsePlayerInfo(args, !hasVoiceFields, out info);
    }

    private static bool TryParsePlayerInfo(
        GetDataEventArgs args,
        bool hasVoiceFields,
        out ParsedPlayerInfo info)
    {
        info = default;

        if (args.Length <= 1 || args.Index < 0 || args.Index + args.Length > args.Msg.readBuffer.Length)
            return false;

        try
        {
            using var ms = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1);
            using var br = new BinaryReader(ms);

            info = new ParsedPlayerInfo
            {
                PlayerId = br.ReadByte(),
                SkinVariant = br.ReadByte()
            };

            if (hasVoiceFields)
            {
                _ = br.ReadByte(); // voiceVariant
                _ = br.ReadSingle(); // voicePitchOffset
            }

            info.Hair = br.ReadByte();
            info.Name = br.ReadString();
            info.HairDye = br.ReadByte();
            info.HideVisualFlags = br.ReadUInt16();
            info.HideMisc = br.ReadByte();

            info.HairColor = ReadColor(br);
            info.SkinColor = ReadColor(br);
            info.EyeColor = ReadColor(br);
            info.ShirtColor = ReadColor(br);
            info.UnderShirtColor = ReadColor(br);
            info.PantsColor = ReadColor(br);
            info.ShoeColor = ReadColor(br);

            info.DifficultyFlags = br.ReadByte();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Color ReadColor(BinaryReader br)
    {
        return new Color(br.ReadByte(), br.ReadByte(), br.ReadByte());
    }

    private struct ParsedPlayerInfo
    {
        public byte PlayerId;
        public int SkinVariant;
        public int Hair;
        public string Name;
        public byte HairDye;
        public ushort HideVisualFlags;
        public byte HideMisc;
        public Color HairColor;
        public Color SkinColor;
        public Color EyeColor;
        public Color ShirtColor;
        public Color UnderShirtColor;
        public Color PantsColor;
        public Color ShoeColor;
        public byte DifficultyFlags;
    }
}
