using Acorn.Database;
using Acorn.Net.Services;
using Acorn.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $qstate - Shows a character's persisted quest progress.
/// </summary>
public class QuestStateCommandHandler(
    INotificationService notifications,
    IWorldQueries world,
    IServiceScopeFactory scopeFactory) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["qstate"];

    public string Usage => "<player> [quest_id]";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $qstate <player> [quest_id]");
            return;
        }

        // Prefer the canonical name when the target is online.
        var name = world.FindPlayerByName(args[0])?.Character?.Name ?? args[0];

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();
        var query = db.QuestProgress.Where(q => q.CharacterName == name);

        if (args.Length > 1)
        {
            if (!int.TryParse(args[1], out var questId))
            {
                await notifications.SystemMessage(playerState, "Quest id must be an integer.");
                return;
            }

            query = query.Where(q => q.QuestId == questId);
        }

        var rows = await query.OrderBy(q => q.QuestId).ToListAsync();
        if (rows.Count == 0)
        {
            await notifications.SystemMessage(playerState, $"No quest progress for '{name}'.");
            return;
        }

        await notifications.SystemMessage(playerState, $"Quest state for {name} ({rows.Count}):");
        foreach (var row in rows)
        {
            await notifications.SystemMessage(playerState,
                $"  Quest {row.QuestId}: state {row.State}, player kills {row.PlayerKills}, completions {row.Completions}");
        }
    }
}
