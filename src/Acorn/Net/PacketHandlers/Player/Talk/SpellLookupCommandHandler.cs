using Acorn.Database.Repository;
using Acorn.Net.Services;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $spellinfo - Looks up spell data by id or name.
/// </summary>
public class SpellLookupCommandHandler(INotificationService notifications, IDataFileRepository dataFiles) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["spellinfo"];

    public string Usage => "<id|name>";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $spellinfo <id|name>");
            return;
        }

        var query = string.Join(" ", args);

        if (int.TryParse(query, out var id))
        {
            var record = dataFiles.Esf.GetSkill(id);
            if (record is null)
            {
                await notifications.SystemMessage(playerState, $"Spell with id {id} not found.");
                return;
            }

            await notifications.SystemMessage(playerState, Describe(id, record));
            return;
        }

        var matches = dataFiles.Esf.Skills
            .Where(s => s.Name is not null && s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            await notifications.SystemMessage(playerState, $"Spell '{query}' not found.");
            return;
        }

        var index = dataFiles.Esf.Skills.IndexOf(matches[0]);
        await notifications.SystemMessage(playerState, Describe(index + 1, matches[0]));
    }

    private static string Describe(int id, EsfRecord record)
        => $"Spell {id}: {record.Name} | tp {record.TpCost}, sp {record.SpCost}, dmg {record.MinDamage}-{record.MaxDamage}, heal {record.HpHeal}";
}
