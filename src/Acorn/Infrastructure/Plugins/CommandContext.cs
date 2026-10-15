using Acorn.Net.Services;
using Acorn.Plugins;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Host-provided <see cref="ICommandContext" /> for plugin chat commands.
/// </summary>
internal sealed class CommandContext(
    PlayerView player,
    string command,
    IReadOnlyList<string> args,
    INotificationService notifications) : ICommandContext
{
    public IPlayerView Player => player;

    public string Command { get; } = command;

    public IReadOnlyList<string> Args { get; } = args;

    public Task ReplyAsync(string message)
    {
        return notifications.SystemMessage(player.Player, message);
    }

    public Task AnnounceAsync(string message)
    {
        return notifications.ServerAnnouncement(player.Player, message);
    }
}
