using System.IO.Hashing;
using System.Text.RegularExpressions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Database.Repository;

public static class EnfExtension
{
    public static EnfRecord? GetNpc(this Enf enf, int id)
    {
        // NPC IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= enf.Npcs.Count ? null : enf.Npcs[index];
    }

    /// <summary>
    ///     Find NPCs by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EnfRecord Npc, int Id)> FindByName(this Enf enf, string name)
    {
        return enf.Npcs
            .Select((npc, index) => (npc, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.npc.Name) &&
                        x.npc.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     Search NPCs by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EnfRecord Npc, int Id)> SearchByName(this Enf enf, string name)
    {
        return enf.Npcs
            .Select((npc, index) => (npc, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.npc.Name) &&
                        x.npc.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
