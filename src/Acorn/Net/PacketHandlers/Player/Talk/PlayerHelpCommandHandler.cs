using Acorn.Net.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     #help - Lists the player commands available to everyone, or shows the usage
///     of a single command.
/// </summary>
public class PlayerHelpCommandHandler(IServiceProvider services, INotificationService notifications)
    : IPlayerCommandHandler
{
    public IReadOnlyList<string> Commands => ["help"];

    public string Usage => "[command]";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length > 0)
        {
            var query = args[0].TrimStart('$', '#');
            var match = services.GetServices<IPlayerCommandHandler>()
                .FirstOrDefault(h => h.CanHandle(query));

            var message = match is not null
                ? CommandHelpFormatter.Describe(match, '#')
                : $"No command named '{args[0]}'.";
            await notifications.SystemMessage(playerState, message);
            return;
        }

        var handlers = services.GetServices<IPlayerCommandHandler>();
        await CommandHelpFormatter.SendPackedAsync(notifications, playerState, "Player commands:",
            CommandHelpFormatter.DescribeAll(handlers, '#'));
    }
}
