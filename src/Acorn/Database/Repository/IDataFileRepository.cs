using System.IO.Hashing;
using System.Text.RegularExpressions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Database.Repository;

public interface IDataFileRepository
{
    Ecf Ecf { get; }
    Eif Eif { get; }
    Enf Enf { get; }
    Esf Esf { get; }
    IEnumerable<MapWithId> Maps { get; }

    /// <summary>
    ///     Re-reads the pub data files (ECF/EIF/ENF/ESF) from disk, replacing the
    ///     in-memory records. Map files are not reloaded.
    /// </summary>
    void Reload();

    /// <summary>
    ///     Re-reads a single <c>{mapId}.emf</c> file from disk and replaces the
    ///     in-memory record for that map. Returns false when the file is missing
    ///     or cannot be deserialized, leaving the previous record in place.
    /// </summary>
    bool TryReloadMap(int mapId, out MapWithId? map);
}
