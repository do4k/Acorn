using Acorn.Extensions;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Plugins;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Bridges a plugin's <see cref="IPluginCommand" /> into the server's player
///     <c>#command</c> dispatcher. One adapter is registered per plugin whose entry
///     type implements <see cref="IPluginCommand" />; it no-ops while the plugin is
///     not loaded or has been disabled.
///     <para>
///         Opted out of the <c>AddAllOfType&lt;IPlayerCommandHandler&gt;()</c>
///         convention scan: its <see cref="PluginEntry" /> constructor dependency is
///         not a container service, so instances can only come from the factory
///         registration in <c>AddPluginCommands</c>.
///     </para>
/// </summary>
[SkipAutoRegistration]
internal sealed class PluginCommandAdapter(
    PluginEntry entry,
    INotificationService notifications,
    ILogger<PluginCommandAdapter> logger) : IPlayerCommandHandler
{
    private IPluginCommand? Command =>
        entry.Disabled ? null : entry.Instance as IPluginCommand;

    public IReadOnlyList<string> Commands => Command?.Commands ?? [];

    public string Usage => Command?.Usage ?? string.Empty;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (Command is not { } pluginCommand)
        {
            return;
        }

        var context = new CommandContext(new PlayerView(playerState), command, args, notifications);
        try
        {
            await pluginCommand.HandleAsync(context);
        }
        catch (Exception ex)
        {
            // Command failures are contained like hook failures: log against the
            // plugin and keep the connection alive. Unlike tick hooks, commands are
            // user-triggered one-offs, so failures are intentionally not counted
            // toward the auto-disable threshold (ConsecutiveHookFailures).
            logger.LogError(ex, "Plugin {PluginId} failed handling command #{Command}",
                entry.Manifest.Id, command);
        }
    }
}
