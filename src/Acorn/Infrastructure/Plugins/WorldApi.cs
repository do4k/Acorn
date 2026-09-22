using Acorn.Net;
using Acorn.Net.Services;
using Acorn.Plugins;
using Acorn.World;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Host implementation of the curated plugin-facing world API. Phase 0 covers
///     presence and messaging; later phases add spawning, broadcasting and items.
/// </summary>
internal sealed class WorldApi(WorldState world, INotificationService notifications) : IWorldApi
{
    public int OnlinePlayerCount => world.Players.Count;

    public IReadOnlyCollection<IPlayerView> OnlinePlayers =>
        world.Players.Values
            .Select(p => (IPlayerView)new PlayerView(p))
            .ToList();

    public Task SendSystemMessageAsync(IPlayerView player, string message)
    {
        return notifications.SystemMessage(Unwrap(player), message);
    }

    public Task SendAnnouncementAsync(IPlayerView player, string message)
    {
        return notifications.ServerAnnouncement(Unwrap(player), message);
    }

    private static PlayerState Unwrap(IPlayerView player)
    {
        return player is PlayerView view
            ? view.Player
            : throw new ArgumentException("Player view was not created by the host.", nameof(player));
    }
}
