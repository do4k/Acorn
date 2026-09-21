using Acorn.Net.Services;
using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     <c>$guild create &lt;tag&gt; &lt;name&gt; [-- &lt;description&gt;]</c> — create a guild with the
///     calling player as the leader, bypassing the normal recruit flow, gold cost and NPC requirement.
/// </summary>
public class GuildCommandHandler(
    IGuildService guildService,
    INotificationService notifications,
    ILogger<GuildCommandHandler> logger) : ITalkHandler
{
    private const string Separator = "--";

    public IReadOnlyList<string> Commands => ["guild"];

    public string Usage => "create <tag> <name> [-- <description>]";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 3 || !args[0].Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            await UsageAsync(playerState);
            return;
        }

        var tag = args[1];

        var separatorIndex = Array.IndexOf(args, Separator, 2);
        string name;
        string description;

        if (separatorIndex >= 0)
        {
            // Need at least one name word before -- and one description word after it.
            if (separatorIndex == 2 || separatorIndex == args.Length - 1)
            {
                await UsageAsync(playerState);
                return;
            }

            name = string.Join(' ', args[2..separatorIndex]);
            description = string.Join(' ', args[(separatorIndex + 1)..]);
        }
        else
        {
            name = string.Join(' ', args[2..]);
            description = string.Empty;
        }

        var displayName = tag.ToUpperInvariant();

        var result = await guildService.AdminCreateGuild(playerState, tag, name, description);

        switch (result)
        {
            case AdminCreateGuildResult.Created:
                logger.LogInformation(
                    "Admin {Player} created guild {Tag} ({Name})",
                    playerState.Character?.Name, displayName, name);
                await notifications.SystemMessage(
                    playerState,
                    $"Guild {displayName} \"{name}\" created with you as leader.");
                break;

            case AdminCreateGuildResult.InvalidInput:
                await notifications.SystemMessage(
                    playerState,
                    "Invalid guild tag, name or description. Tags are 2–3 letters; names are letters and spaces (4+ chars); descriptions are lowercase letters, digits and @ _ - ..");
                break;

            case AdminCreateGuildResult.AlreadyInGuild:
                await notifications.SystemMessage(
                    playerState,
                    "You are already in a guild. Leave it first.");
                break;

            case AdminCreateGuildResult.GuildExists:
                await notifications.SystemMessage(
                    playerState,
                    "A guild with that tag or name already exists.");
                break;
        }
    }

    private Task UsageAsync(PlayerState playerState)
        => notifications.SystemMessage(playerState, $"Usage: $guild {Usage}");
}
