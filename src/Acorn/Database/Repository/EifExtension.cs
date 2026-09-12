using System.IO.Hashing;
using System.Text.RegularExpressions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Database.Repository;

public static class EifExtension
{
    public static EifRecord? GetItem(this Eif eif, int id)
    {
        // Item IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= eif.Items.Count ? null : eif.Items[index];
    }

    /// <summary>
    ///     Find items by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EifRecord Item, int Id)> FindByName(this Eif eif, string name)
    {
        return eif.Items
            .Select((item, index) => (item, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.item.Name) &&
                        x.item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     Search items by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EifRecord Item, int Id)> SearchByName(this Eif eif, string name)
    {
        return eif.Items
            .Select((item, index) => (item, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.item.Name) &&
                        x.item.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
