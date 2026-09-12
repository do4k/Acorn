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
}
