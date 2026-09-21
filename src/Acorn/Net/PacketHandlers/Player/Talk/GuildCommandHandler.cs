using Acorn.Net.Services;
using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     <c>$guild create &lt;tag&gt; &lt;name&gt;</c> — create a guild with the calling player as
///     the leader, bypassing the normal recruit flow, gold cost and NPC requirement.
/// </summary>
public class GuildCommandHandler(
    IGuildService guildService,
    INotificationService notifications,
    ILogger<GuildCommandHandler> logger) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["guild"];

    public string Usage => "create <tag> <name>";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 3 || !args[0].Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            await notifications.SystemMessage(playerState, "Usage: $guild create <tag> <name>");
            return;
        }

        var tag = args[1];
        var name = string.Join(' ', args[2..]);
        var displayName = tag.ToUpperInvariant();

        var result = await guildService.AdminCreateGuild(playerState, tag, name);

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

            case AdminCreateGuildResult.InvalidTagOrName:
                await notifications.SystemMessage(
                    playerState,
                    "Invalid guild tag or name. Tags are 2–3 uppercase letters; names are lowercase words (4+ chars).");
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
}
