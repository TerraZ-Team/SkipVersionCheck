using System.Text;

using Terraria;
using Terraria.ID;
using TerrariaApi.Server;

using TShockAPI;

namespace SkipVersionCheck;

/// <summary>
/// Entry point plugin. Keeps hook wiring and delegates packet behavior to
/// dedicated handlers.
/// </summary>
[ApiVersion(2, 1)]
public sealed class SkipVersionCheck : TerrariaPlugin
{
    private readonly ClientVersionStore _clientVersions;
    private readonly ConnectRequestHandler _connectRequestHandler;
    private readonly PlayerInfoHandler _playerInfoHandler;
    private readonly SpawnPacketHandler _spawnPacketHandler;
    private readonly ItemDropHandler _itemDropHandler;

    private int _serverMaxItemId;
    private PluginConfig _config = new();

    public static SkipVersionCheck? Instance { get; private set; }
    public bool IsDebugLoggingEnabled => _config.DebugLogging;

    public override string Name => "SkipVersionCheck";
    public override string Author => "Jgran";
    public override string Description =>
        "Allows compatible Terraria clients to connect regardless of exact patch version, " +
        "with protocol translation for cross-version play.";
    public override Version Version => new(2, 13, 3);

    public SkipVersionCheck(Main game) : base(game)
    {
        Order = -1;
        Instance = this;

        _clientVersions = new ClientVersionStore();
        _connectRequestHandler = new ConnectRequestHandler(_clientVersions);
        _playerInfoHandler = new PlayerInfoHandler(_clientVersions);
        _spawnPacketHandler = new SpawnPacketHandler(_clientVersions);
        _itemDropHandler = new ItemDropHandler(_clientVersions);
    }

    public override void Initialize()
    {
        _config = PluginConfig.Load();

        On.Terraria.Net.NetManager.Broadcast_NetPacket_int += NetModuleHandler.OnBroadcast;
        On.Terraria.Net.NetManager.SendToClient += NetModuleHandler.OnSendToClient;

        ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MinValue);
        ServerApi.Hooks.NetGetData.Register(this, OnGetDataLate, int.MaxValue);
        ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
        ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        ServerApi.Hooks.NetSendData.Register(this, OnSendData, int.MinValue);

        if (_config.DebugLogging)
        {
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin, int.MinValue);
        }

        Commands.ChatCommands.Add(new Command("skipversioncheck.admin", ReloadCommand, "svcreload")
        {
            HelpText = "Reloads the SkipVersionCheck configuration."
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            On.Terraria.Net.NetManager.Broadcast_NetPacket_int -= NetModuleHandler.OnBroadcast;
            On.Terraria.Net.NetManager.SendToClient -= NetModuleHandler.OnSendToClient;

            ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            ServerApi.Hooks.NetGetData.Deregister(this, OnGetDataLate);
            ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
            ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
            ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);

            Commands.ChatCommands.RemoveAll(c => c.Names.Contains("svcreload"));
        }

        base.Dispose(disposing);
    }

    private void ReloadCommand(CommandArgs args)
    {
        _config = PluginConfig.Load();
        args.Player.SendSuccessMessage(
            "[SkipVersionCheck] Configuration reloaded. " +
            $"DebugLogging={_config.DebugLogging}, " +
            $"MinSupportedRelease={_config.MinSupportedRelease}, " +
            $"SupportJourneyClients={_config.SupportJourneyClients}");
    }

    private void OnPostInitialize(EventArgs args)
    {
        _serverMaxItemId = ItemID.Count;

        if (!_config.DebugLogging)
            return;

        StringBuilder sb = new StringBuilder()
            .Append("[SkipVersionCheck] Active - ")
            .Append($"Server curRelease: {Main.curRelease}, ")
            .Append($"versionNumber: {Main.versionNumber}, ")
            .Append($"maxItemId: {_serverMaxItemId}\n")
            .Append("[SkipVersionCheck] Known versions: ")
            .Append(string.Join(", ", VersionCatalog.GetKnownVersionNames()));
        sb.Append("\n[SkipVersionCheck] Debug logging is ENABLED.");

        TShock.Log.ConsoleInfo(sb.ToString());
    }

    private void OnLeave(LeaveEventArgs args)
    {
        int previousVersion = _clientVersions.Clear(args.Who);
        if (previousVersion > 0 && _config.DebugLogging)
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] Cross-version client {args.Who} " +
                $"(release {previousVersion}) disconnected.");
        }
    }

    private void OnServerJoin(JoinEventArgs args)
    {
        if (!_config.DebugLogging)
            return;

        int who = args.Who;
        if (who >= 0 && who < 256 && _clientVersions.IsCrossVersion(who, Main.curRelease))
        {
            TShock.Log.ConsoleInfo(
                $"[SkipVersionCheck] DEBUG ServerJoin: client={who}, " +
                $"state={Netplay.Clients[who].State}, handled={args.Handled}");
        }
    }

    private void OnSendData(SendDataEventArgs args)
    {
        if (args.Handled)
            return;

        if (_config.DebugLogging)
        {
            int target = args.remoteClient;
            if (target >= 0 && target < 256)
            {
                int clientState = Netplay.Clients[target].State;
                if (clientState < 10 && _clientVersions.IsCrossVersion(target, Main.curRelease))
                {
                    TShock.Log.ConsoleInfo(
                        $"[SkipVersionCheck] DEBUG SEND pkt={args.MsgId}({(int)args.MsgId}) " +
                        $"to={target} state={clientState}");
                }
            }
        }

        if (args.MsgId == PacketTypes.PlayerSpawn)
            _spawnPacketHandler.HandleOutgoing(args, _config);
    }

    private void OnGetData(GetDataEventArgs args)
    {
        if (args.Handled)
            return;

        if (_config.DebugLogging)
        {
            int who = args.Msg.whoAmI;
            if (who >= 0 && who < 256)
            {
                int clientState = Netplay.Clients[who].State;
                if (clientState < 10)
                {
                    TShock.Log.ConsoleInfo(
                        $"[SkipVersionCheck] DEBUG RECV pkt={args.MsgID}({(int)args.MsgID}) " +
                        $"client={who} state={clientState}");
                }
            }
        }

        switch (args.MsgID)
        {
            case PacketTypes.ConnectRequest:
                _connectRequestHandler.Handle(args, _config);
                break;

            case PacketTypes.PlayerInfo:
                _playerInfoHandler.Handle(args, _config);
                break;

            case PacketTypes.PlayerSpawn:
                _spawnPacketHandler.HandleIncoming(args, _config);
                break;

            case PacketTypes.ItemDrop:
                _itemDropHandler.HandleIncoming(args, _serverMaxItemId, _config);
                break;
        }
    }

    private void OnGetDataLate(GetDataEventArgs args)
    {
        if (args.Handled)
            return;

        _playerInfoHandler.HandleLateFallback(args, _config);
    }

    // Used by NetModuleHandler for outgoing NetModule filtering.
    public int GetClientVersion(int playerIndex)
    {
        return _clientVersions.GetVersion(playerIndex);
    }

    // Used by NetModuleHandler for item compatibility checks.
    public int GetMaxItemsForVersion(int clientVersion)
    {
        return VersionCatalog.GetMaxItemsForVersion(clientVersion, _serverMaxItemId);
    }
}

