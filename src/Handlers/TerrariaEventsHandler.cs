namespace SkipVersionCheck.Handlers;

internal static class TerrariaEventsHandler
{
    public static void RegisterHandlers()
    {
        On.Terraria.Net.NetManager.Broadcast_NetPacket_int += NetModuleHandler.OnBroadcast;
        On.Terraria.Net.NetManager.SendToClient += NetModuleHandler.OnSendToClient;
    }

    public static void UnregisterHandlers()
    {
        On.Terraria.Net.NetManager.Broadcast_NetPacket_int -= NetModuleHandler.OnBroadcast;
        On.Terraria.Net.NetManager.SendToClient -= NetModuleHandler.OnSendToClient;
    }
}
