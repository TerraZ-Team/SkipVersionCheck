using Terraria;
using TerrariaApi.Server;
using SkipVersionCheck.Handlers;

namespace SkipVersionCheck;

[ApiVersion(2, 1)]
public sealed class SkipVersionCheck : TerrariaPlugin
{
    public static SkipVersionCheck? Instance { get; private set; }

    public override string Name => "SkipVersionCheck";
    public override string Author => "Jgran";
    public override string Description =>
        "Allows compatible Terraria clients to connect regardless of exact patch version, " +
        "with protocol translation for cross-version play.";
    public override Version Version => new(2, 14, 0);

    public SkipVersionCheck(Main game) : base(game)
    {
        Order = -1;
        Instance = this;
    }

    public override void Initialize()
    {
        TerrariaEventsHandler.RegisterHandlers();
        TShockEventsHandler.RegisterHandlers();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TShockEventsHandler.UnregisterHandlers();
            TerrariaEventsHandler.UnregisterHandlers();
        }

        base.Dispose(disposing);
    }
}
