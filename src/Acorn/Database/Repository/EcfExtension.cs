using System.IO.Hashing;
using System.Text.RegularExpressions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Database.Repository;

public static class EcfExtension
{
    public static EcfRecord? GetClass(this Ecf ecf, int id)
    {
        // Class IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= ecf.Classes.Count ? null : ecf.Classes[index];
    }

    /// <summary>
    ///     Find classes by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EcfRecord Class, int Id)> FindByName(this Ecf ecf, string name)
    {
        return ecf.Classes
            .Select((cls, index) => (cls, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.cls.Name) &&
                        x.cls.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     Search classes by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EcfRecord Class, int Id)> SearchByName(this Ecf ecf, string name)
    {
        return ecf.Classes
            .Select((cls, index) => (cls, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.cls.Name) &&
                        x.cls.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
