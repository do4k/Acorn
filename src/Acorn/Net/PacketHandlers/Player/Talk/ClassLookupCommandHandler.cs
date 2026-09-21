using Acorn.Database.Repository;
using Acorn.Net.Services;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $class - Looks up class data by id or name.
/// </summary>
public class ClassLookupCommandHandler(INotificationService notifications, IDataFileRepository dataFiles) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["class"];

    public string Usage => "<id|name>";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $class <id|name>");
            return;
        }

        var query = string.Join(" ", args);
        var classes = dataFiles.Ecf.Classes;

        if (int.TryParse(query, out var id))
        {
            // Class ids are 1-based.
            if (id < 1 || id > classes.Count)
            {
                await notifications.SystemMessage(playerState, $"Class with id {id} not found.");
                return;
            }

            await notifications.SystemMessage(playerState, Describe(id, classes[id - 1]));
            return;
        }

        var index = classes.FindIndex(c =>
            c.Name is not null && c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            await notifications.SystemMessage(playerState, $"Class '{query}' not found.");
            return;
        }

        await notifications.SystemMessage(playerState, Describe(index + 1, classes[index]));
    }

    private static string Describe(int id, EcfRecord record)
        => $"Class {id}: {record.Name} | str {record.Str}, int {record.Intl}, wis {record.Wis}, agi {record.Agi}, con {record.Con}, cha {record.Cha}";
}
