using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Map;

/// <summary>
///     Result of an item drop operation.
/// </summary>
public record ItemDropResult(bool Success, int? ItemIndex = null, string? ErrorMessage = null);
