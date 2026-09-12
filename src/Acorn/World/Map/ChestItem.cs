using System.Collections.Concurrent;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Map;

/// <summary>
/// Represents an item in a map chest
/// </summary>
public record ChestItem(int ItemId, int Amount);
