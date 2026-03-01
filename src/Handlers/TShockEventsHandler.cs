using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using SkipVersionCheck.Configuration;

namespace SkipVersionCheck.Handlers;

internal static class TShockEventsHandler
{
    private static readonly Command ReloadCommand = new("skipversioncheck.admin", OnReloadCommand, "svcreload")
    {
        HelpText = "Reloads the SkipVersionCheck configuration."
    };

    public static void RegisterHandlers()
    {
        if (SkipVersionCheck.Instance == null)
            return;

        ServerApi.Hooks.GameInitialize.Register(SkipVersionCheck.Instance, OnInitialize);
        ServerApi.Hooks.GamePostInitialize.Register(SkipVersionCheck.Instance, OnPostInitialize);
        ServerApi.Hooks.NetGetData.Register(SkipVersionCheck.Instance, OnGetData, int.MinValue);
        ServerApi.Hooks.NetSendData.Register(SkipVersionCheck.Instance, OnSendData, int.MinValue);
        ServerApi.Hooks.ServerLeave.Register(SkipVersionCheck.Instance, OnLeave);
        GeneralHooks.ReloadEvent += OnReload;

        Commands.ChatCommands.Add(ReloadCommand);
    }

    public static void UnregisterHandlers()
    {
        if (SkipVersionCheck.Instance == null)
            return;

        ServerApi.Hooks.GameInitialize.Deregister(SkipVersionCheck.Instance, OnInitialize);
        ServerApi.Hooks.GamePostInitialize.Deregister(SkipVersionCheck.Instance, OnPostInitialize);
        ServerApi.Hooks.NetGetData.Deregister(SkipVersionCheck.Instance, OnGetData);
        ServerApi.Hooks.NetSendData.Deregister(SkipVersionCheck.Instance, OnSendData);
        ServerApi.Hooks.ServerLeave.Deregister(SkipVersionCheck.Instance, OnLeave);
        GeneralHooks.ReloadEvent -= OnReload;

        Commands.ChatCommands.RemoveAll(c => c == ReloadCommand || c.Names.Contains("svcreload"));
    }

    private static void OnInitialize(EventArgs args) => Config.Reload();

    private static void OnPostInitialize(EventArgs args)
    {
        SkipVersionState.ServerMaxItemId = ItemID.Count;
    }

    private static void OnReload(ReloadEventArgs args)
    {
        Config.Reload();
    }

    private static void OnReloadCommand(CommandArgs args)
    {
        Config.Reload();
        ConfigSettings settings = Config.Settings;
        args.Player.SendSuccessMessage(
            "[SkipVersionCheck] Configuration reloaded. " +
            $"DebugLogging={settings.DebugLogging}, " +
            $"MinSupportedRelease={settings.MinSupportedRelease}, " +
            $"SupportJourneyClients={settings.SupportJourneyClients}");
    }

    private static void OnLeave(LeaveEventArgs args)
    {
        ClientVersionStore.Clear(args.Who);
    }

    private static void OnSendData(SendDataEventArgs args)
    {
        if (args.Handled)
            return;

        if (args.MsgId == PacketTypes.PlayerSpawn)
            SpawnPacketHandler.HandleOutgoing(args);
    }

    private static void OnGetData(GetDataEventArgs args)
    {
        if (args.Handled)
            return;

        ConfigSettings settings = Config.Settings;
        switch (args.MsgID)
        {
            case PacketTypes.ConnectRequest:
                ConnectRequestHandler.Handle(args, settings);
                break;
            case PacketTypes.PlayerInfo:
                PlayerInfoHandler.Handle(args, settings);
                break;
            case PacketTypes.PlayerSpawn:
                SpawnPacketHandler.HandleIncoming(args);
                break;
            case PacketTypes.ItemDrop:
                ItemDropHandler.HandleIncoming(args, SkipVersionState.ServerMaxItemId);
                break;
        }
    }
}
