using Acorn.Plugins;
using Microsoft.Extensions.Logging;

namespace HelloAcorn;

/// <summary>
///     Sample plugin demonstrating the Phase 0 surface: lifecycle
///     (<see cref="IAcornPlugin" />), world and map tick hooks, and a player chat
///     command (<c>#hello</c>).
/// </summary>
public sealed class HelloAcornPlugin : IAcornPlugin, IWorldTickHook, IMapTickHook, IPluginCommand
{
    /// <summary>Log a heartbeat every N world ticks (~10 ticks = 10s at the default 1000ms tick rate).</summary>
    private const int HeartbeatTickInterval = 10;

    private IPluginContext? _context;

    /// <inheritdoc />
    public IReadOnlyList<string> Commands => ["hello"];

    /// <inheritdoc />
    public Task OnLoadedAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        _context = context;
        _context.Log.LogInformation(
            "Hello Acorn {Version} loaded (plugin contract v{ContractVersion})",
            context.PluginVersion,
            PluginContract.CurrentVersion);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnUnloadingAsync(CancellationToken cancellationToken)
    {
        _context?.Log.LogInformation("Hello Acorn unloading");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnWorldTickAsync(WorldTickContext context)
    {
        if (context.TotalTicks % HeartbeatTickInterval == 0)
        {
            _context?.Log.LogDebug(
                "World tick {TotalTicks}: {Players} player(s) online",
                context.TotalTicks,
                context.OnlinePlayerCount);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnMapTickAsync(MapTickContext context)
    {
        // Demonstrates the per-map hook. Intentionally a no-op: map hooks run for
        // every map on every tick, so anything placed here must stay cheap.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ICommandContext context)
    {
        if (_context is null)
        {
            return;
        }

        var name = context.Player.Name ?? "traveler";
        await context.ReplyAsync(
            $"Hello, {name}! You are on map {context.Player.MapId} at ({context.Player.X},{context.Player.Y}). " +
            $"{_context.World.OnlinePlayerCount} player(s) online.");
    }
}
