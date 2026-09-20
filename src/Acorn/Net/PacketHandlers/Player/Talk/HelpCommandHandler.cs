using Acorn.Net.Services;
using Microsoft.Extensions.DependencyInjection;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $help - Lists the admin commands available to the caller, or shows the usage
///     of a single command.
/// </summary>
public class HelpCommandHandler(IServiceProvider services, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["help"];

    public string Usage => "[command]";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var admin = playerState.Character?.Admin ?? AdminLevel.Player;

        if (args.Length > 0)
        {
            await ShowCommandUsage(playerState, args[0], admin);
            return;
        }

        var adminHandlers = services.GetServices<ITalkHandler>()
            .Where(h => admin >= h.RequiredLevel);
        await CommandHelpFormatter.SendPackedAsync(notifications, playerState, "Admin commands:",
            CommandHelpFormatter.DescribeAll(adminHandlers, '$'));

        var playerHandlers = services.GetServices<IPlayerCommandHandler>();
        await CommandHelpFormatter.SendPackedAsync(notifications, playerState, "Player commands:",
            CommandHelpFormatter.DescribeAll(playerHandlers, '#'));
    }

    private async Task ShowCommandUsage(PlayerState playerState, string rawCommand, AdminLevel admin)
    {
        var query = rawCommand.TrimStart('$', '#');

        var adminMatch = services.GetServices<ITalkHandler>()
            .FirstOrDefault(h => h.CanHandle(query) && admin >= h.RequiredLevel);
        if (adminMatch is not null)
        {
            await notifications.SystemMessage(playerState, CommandHelpFormatter.Describe(adminMatch, '$'));
            return;
        }

        var playerMatch = services.GetServices<IPlayerCommandHandler>()
            .FirstOrDefault(h => h.CanHandle(query));
        if (playerMatch is not null)
        {
            await notifications.SystemMessage(playerState, CommandHelpFormatter.Describe(playerMatch, '#'));
            return;
        }

        await notifications.SystemMessage(playerState, $"No command named '{rawCommand}'.");
    }
}
