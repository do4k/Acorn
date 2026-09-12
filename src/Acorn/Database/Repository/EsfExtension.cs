using System.IO.Hashing;
using System.Text.RegularExpressions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Database.Repository;

public static class EsfExtension
{
    public static EsfRecord? GetSkill(this Esf esf, int id)
    {
        // Skill IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= esf.Skills.Count ? null : esf.Skills[index];
    }

    /// <summary>
    ///     Find skills by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EsfRecord Skill, int Id)> FindByName(this Esf esf, string name)
    {
        return esf.Skills
            .Select((skill, index) => (skill, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.skill.Name) &&
                        x.skill.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     Search skills by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EsfRecord Skill, int Id)> SearchByName(this Esf esf, string name)
    {
        return esf.Skills
            .Select((skill, index) => (skill, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.skill.Name) &&
                        x.skill.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
