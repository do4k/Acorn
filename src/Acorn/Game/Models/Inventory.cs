using System.Collections.Concurrent;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Game.Models;

public record Inventory(ConcurrentBag<ItemWithAmount> Items);
