using System.IO;
using TShockAPI;

namespace SkipVersionCheck.Handlers;

internal static class TerrariaEventsHandler
{
    private static bool _monoModHooksRegistered;

    public static void RegisterHandlers()
    {
        try
        {
            On.Terraria.Net.NetManager.Broadcast_NetPacket_int += NetModuleHandler.OnBroadcast;
            On.Terraria.Net.NetManager.SendToClient += NetModuleHandler.OnSendToClient;
            _monoModHooksRegistered = true;
        }
        catch (Exception ex) when (ex is TypeLoadException or MissingMethodException or FileNotFoundException or FileLoadException)
        {
            _monoModHooksRegistered = false;
            TShock.Log.ConsoleWarn(
                "[SkipVersionCheck] MonoMod hooks unavailable; outgoing NetModule packet filtering is disabled. " +
                $"Reason: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void UnregisterHandlers()
    {
        if (!_monoModHooksRegistered)
            return;

        On.Terraria.Net.NetManager.Broadcast_NetPacket_int -= NetModuleHandler.OnBroadcast;
        On.Terraria.Net.NetManager.SendToClient -= NetModuleHandler.OnSendToClient;
        _monoModHooksRegistered = false;
    }
}
