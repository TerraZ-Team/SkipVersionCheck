using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using SkipVersionCheck.Configuration;

namespace SkipVersionCheck;

internal static class PlayerInfoHandler
{
    public static void Handle(GetDataEventArgs args, ConfigSettings config)
    {
        if (args.MsgID != PacketTypes.PlayerInfo)
            return;

        int who = args.Msg.whoAmI;
        if (!ClientVersionStore.IsCrossVersion(who, Main.curRelease))
            return;

        // Original strategy: suppress vanilla PlayerInfo path for all cross-version clients
        // and apply parsed values manually, including appearance data.
        args.Handled = true;

        int clientRelease = ClientVersionStore.GetVersion(who);
        if (!TryParsePlayerInfo(args, clientRelease, out ParsedPlayerInfo info))
        {
            AdvanceHandshakeIfNeeded(who);
            return;
        }

        ApplyParsedPlayerInfo(who, info, config);
        AdvanceHandshakeIfNeeded(who);
    }

    private static void ApplyParsedPlayerInfo(int who, ParsedPlayerInfo info, ConfigSettings config)
    {
        Player? player = Main.player[who];
        if (player == null)
            return;

        if (!string.IsNullOrWhiteSpace(info.Name))
            player.name = info.Name;

        player.skinVariant = info.SkinVariant;
        player.voiceVariant = info.VoiceVariant;
        player.voicePitchOffset = info.VoicePitchOffset;
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

        byte difficultyFlags = NormalizeJourneyFlag(info.DifficultyFlags, who, config, out bool journeyChanged);
        player.difficulty = (byte)(difficultyFlags & 0b11);

        bool hasExtraAccessory = ((PlayerInfoDifficultyFlags)difficultyFlags & PlayerInfoDifficultyFlags.ExtraAccessory) != 0;
        if (hasExtraAccessory && !player.extraAccessory)
            player.extraAccessory = true;

        if (config.DebugLogging)
        {
            bool journeyEnabled = ((PlayerInfoDifficultyFlags)difficultyFlags & PlayerInfoDifficultyFlags.Creative) != 0;
            string journeyAdjustment = journeyChanged ? ", journeyAdjusted=true" : string.Empty;
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] Client {who} PlayerInfo: extraSlot={hasExtraAccessory}, journey={journeyEnabled}, diff=0x{difficultyFlags:X2}{journeyAdjustment}");
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

    private static byte NormalizeJourneyFlag(byte difficultyFlags, int who, ConfigSettings config, out bool changed)
    {
        changed = false;
        if (!config.SupportJourneyClients)
            return difficultyFlags;

        PlayerInfoDifficultyFlags flags = (PlayerInfoDifficultyFlags)difficultyFlags;

        if (Main.GameMode == GameModeID.Creative)
        {
            if ((flags & PlayerInfoDifficultyFlags.Creative) == 0)
            {
                flags |= PlayerInfoDifficultyFlags.Creative;
                changed = true;
                if (Main.ServerSideCharacter)
                    NetMessage.SendData((int)PacketTypes.PlayerInfo, who, -1, null, who);
            }

            return (byte)flags;
        }

        if ((flags & PlayerInfoDifficultyFlags.Creative) != 0)
        {
            flags &= ~PlayerInfoDifficultyFlags.Creative;
            changed = true;
        }

        return (byte)flags;
    }

    private static void AdvanceHandshakeIfNeeded(int who)
    {
        if (who < 0 || who >= 256)
            return;

        if (Netplay.Clients[who].State != 1)
            return;

        Netplay.Clients[who].State = 2;
        NetMessage.SendData((int)PacketTypes.WorldInfo, who);
    }

    private static bool TryParsePlayerInfo(
        GetDataEventArgs args,
        int clientRelease,
        out ParsedPlayerInfo info)
    {
        bool hasVoiceFields = VersionCatalog.Supports(clientRelease, ClientFeatures.PlayerInfoVoiceV2);
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
            using MemoryStream ms = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1);
            using BinaryReader br = new BinaryReader(ms);

            info = new ParsedPlayerInfo
            {
                PlayerId = br.ReadByte(),
                SkinVariant = br.ReadByte()
            };

            if (hasVoiceFields)
            {
                info.VoiceVariant = br.ReadByte();
                info.VoicePitchOffset = br.ReadSingle();
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
        public byte VoiceVariant;
        public float VoicePitchOffset;
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

[Flags]
internal enum PlayerInfoDifficultyFlags : byte
{
    None = 0,
    ExtraAccessory = 1 << 2,
    Creative = 1 << 3,
}
