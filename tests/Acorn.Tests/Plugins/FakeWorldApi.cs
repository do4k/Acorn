using Acorn.Plugins;

namespace Acorn.Tests.Plugins;

/// <summary>
///     In-memory <see cref="IWorldApi" /> capturing outbound messages.
/// </summary>
internal sealed class FakeWorldApi : IWorldApi
{
    public int OnlinePlayerCount => 0;

    public IReadOnlyCollection<IPlayerView> OnlinePlayers => [];

    public List<(IPlayerView Player, string Message)> SystemMessages { get; } = [];

    public Task SendSystemMessageAsync(IPlayerView player, string message)
    {
        SystemMessages.Add((player, message));
        return Task.CompletedTask;
    }

    public Task SendAnnouncementAsync(IPlayerView player, string message)
    {
        SystemMessages.Add((player, message));
        return Task.CompletedTask;
    }
}
