using System.Collections.Concurrent;

namespace Acorn.Game.Models;

public record Spells(ConcurrentBag<Spell> Items);
