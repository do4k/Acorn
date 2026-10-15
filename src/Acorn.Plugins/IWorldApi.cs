namespace Acorn.Plugins;

/// <summary>
///     Curated API onto the game world, exposed to plugins through
///     <see cref="IPluginContext.World" />. Grows in later phases (NPC spawning,
///     broadcasting, item drops); Phase 0 covers presence and messaging.
/// </summary>
public interface IWorldApi
{
    /// <summary>Number of currently connected players.</summary>
    int OnlinePlayerCount { get; }

    /// <summary>Snapshot of the currently connected players.</summary>
    IReadOnlyCollection<IPlayerView> OnlinePlayers { get; }

    /// <summary>
    ///     Sends a private system message to a player (shown only to them).
    /// </summary>
    Task SendSystemMessageAsync(IPlayerView player, string message);

    /// <summary>
    ///     Sends a server announcement to a player (shown with the server prefix).
    /// </summary>
    Task SendAnnouncementAsync(IPlayerView player, string message);
}
