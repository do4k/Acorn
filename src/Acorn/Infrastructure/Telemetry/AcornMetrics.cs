using System.Diagnostics.Metrics;

namespace Acorn.Infrastructure.Telemetry;

/// <summary>
/// Centralized game server metrics using System.Diagnostics.Metrics.
/// Instruments are named following OpenTelemetry semantic conventions.
/// </summary>
public sealed class AcornMetrics : IDisposable
{
    public const string MeterName = "Acorn";

    private readonly Meter _meter;

    public AcornMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        // --- Connection lifecycle ---
        ConnectionsTotal = _meter.CreateCounter<long>(
            "acorn.connections.total",
            description: "Total player connections accepted");

        DisconnectionsTotal = _meter.CreateCounter<long>(
            "acorn.disconnections.total",
            description: "Total player disconnections");

        PlayersOnline = _meter.CreateUpDownCounter<long>(
            "acorn.players.online",
            description: "Current number of connected players");

        // --- Account & character lifecycle ---
        AccountsCreated = _meter.CreateCounter<long>(
            "acorn.accounts.created",
            description: "Total accounts created");

        CharactersCreated = _meter.CreateCounter<long>(
            "acorn.characters.created",
            description: "Total characters created");

        LoginsTotal = _meter.CreateCounter<long>(
            "acorn.logins.total",
            description: "Total successful logins");

        // --- Combat ---
        NpcKills = _meter.CreateCounter<long>(
            "acorn.npc.kills",
            description: "Total NPCs killed by players");

        ExperienceGained = _meter.CreateCounter<long>(
            "acorn.experience.gained",
            description: "Total experience points gained by players");

        LevelUps = _meter.CreateCounter<long>(
            "acorn.levelups.total",
            description: "Total player level-ups");

        // --- Economy ---
        GoldEarned = _meter.CreateCounter<long>(
            "acorn.gold.earned",
            "{gold}",
            "Total gold earned by players (shop sales, NPC drops)");

        GoldSpent = _meter.CreateCounter<long>(
            "acorn.gold.spent",
            "{gold}",
            "Total gold spent by players (shop purchases, services)");

        ItemsDropped = _meter.CreateCounter<long>(
            "acorn.items.dropped",
            description: "Total items dropped to ground by players");

        ItemsPickedUp = _meter.CreateCounter<long>(
            "acorn.items.picked_up",
            description: "Total items picked up from ground by players");

        ItemsJunked = _meter.CreateCounter<long>(
            "acorn.items.junked",
            description: "Total items destroyed by players");

        TradesCompleted = _meter.CreateCounter<long>(
            "acorn.trades.completed",
            description: "Total trades completed between players");

        // --- Performance ---
        MapTickDuration = _meter.CreateHistogram<double>(
            "acorn.map.tick.duration",
            "ms",
            "Duration of map tick processing in milliseconds");

        PacketProcessDuration = _meter.CreateHistogram<double>(
            "acorn.packet.process.duration",
            "ms",
            "Duration of packet handler processing in milliseconds");

        PacketsRateLimited = _meter.CreateCounter<long>(
            "acorn.packets.rate_limited",
            description: "Total packets rejected by rate limiter");

        PacketsUnhandled = _meter.CreateCounter<long>(
            "acorn.packets.unhandled",
            description: "Total packets with no registered handler");
    }

    // Connection lifecycle
    public Counter<long> ConnectionsTotal { get; }
    public Counter<long> DisconnectionsTotal { get; }
    public UpDownCounter<long> PlayersOnline { get; }

    // Account & character lifecycle
    public Counter<long> AccountsCreated { get; }
    public Counter<long> CharactersCreated { get; }
    public Counter<long> LoginsTotal { get; }

    // Combat
    public Counter<long> NpcKills { get; }
    public Counter<long> ExperienceGained { get; }
    public Counter<long> LevelUps { get; }

    // Economy
    public Counter<long> GoldEarned { get; }
    public Counter<long> GoldSpent { get; }
    public Counter<long> ItemsDropped { get; }
    public Counter<long> ItemsPickedUp { get; }
    public Counter<long> ItemsJunked { get; }
    public Counter<long> TradesCompleted { get; }

    // Performance
    public Histogram<double> MapTickDuration { get; }
    public Histogram<double> PacketProcessDuration { get; }
    public Counter<long> PacketsRateLimited { get; }
    public Counter<long> PacketsUnhandled { get; }

    public void Dispose() => _meter.Dispose();
}
