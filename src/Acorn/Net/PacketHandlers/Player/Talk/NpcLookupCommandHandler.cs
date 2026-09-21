using Acorn.Database.Repository;
using Acorn.Net.Services;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $npc - Looks up NPC data by id or name.
/// </summary>
public class NpcLookupCommandHandler(INotificationService notifications, IDataFileRepository dataFiles) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["npc"];

    public string Usage => "<id|name>";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $npc <id|name>");
            return;
        }

        var query = string.Join(" ", args);

        if (int.TryParse(query, out var id))
        {
            var record = dataFiles.Enf.GetNpc(id);
            if (record is null)
            {
                await notifications.SystemMessage(playerState, $"NPC with id {id} not found.");
                return;
            }

            await notifications.SystemMessage(playerState, Describe(id, record));
            return;
        }

        var matches = dataFiles.Enf.FindByName(query);
        if (matches.Count == 0)
        {
            matches = dataFiles.Enf.SearchByName(query);
        }

        if (matches.Count == 0)
        {
            await notifications.SystemMessage(playerState, $"NPC '{query}' not found.");
            return;
        }

        var best = matches[0];
        await notifications.SystemMessage(playerState, Describe(best.Id, best.Npc));
    }

    private static string Describe(int id, EnfRecord record)
        => $"NPC {id}: {record.Name} | hp {record.Hp}, exp {record.Experience}, dmg {record.MinDamage}-{record.MaxDamage}";
}
