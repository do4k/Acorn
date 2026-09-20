using Acorn.Database.Repository;
using Acorn.Net.Services;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $item - Looks up item data by id or name.
/// </summary>
public class ItemLookupCommandHandler(INotificationService notifications, IDataFileRepository dataFiles) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["item"];

    public string Usage => "<id|name>";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $item <id|name>");
            return;
        }

        var query = string.Join(" ", args);

        if (int.TryParse(query, out var id))
        {
            var record = dataFiles.Eif.GetItem(id);
            if (record is null)
            {
                await notifications.SystemMessage(playerState, $"Item with id {id} not found.");
                return;
            }

            await notifications.SystemMessage(playerState, Describe(id, record));
            return;
        }

        var matches = dataFiles.Eif.FindByName(query);
        if (matches.Count == 0)
        {
            matches = dataFiles.Eif.SearchByName(query);
        }

        if (matches.Count == 0)
        {
            await notifications.SystemMessage(playerState, $"Item '{query}' not found.");
            return;
        }

        var best = matches[0];
        await notifications.SystemMessage(playerState, Describe(best.Id, best.Item));
    }

    private static string Describe(int id, EifRecord record)
        => $"Item {id}: {record.Name} | type {record.Type}, weight {record.Weight}, level req {record.LevelRequirement}, class req {record.ClassRequirement}";
}
