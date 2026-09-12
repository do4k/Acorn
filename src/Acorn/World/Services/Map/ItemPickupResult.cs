using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Map;

/// <summary>
///     Result of an item pickup operation.
/// </summary>
public record ItemPickupResult(bool Success, int ItemId = 0, int Amount = 0, string? ErrorMessage = null);
