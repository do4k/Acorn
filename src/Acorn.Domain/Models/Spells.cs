using System.Collections.Concurrent;

namespace Acorn.Domain.Models;

public record Spell(int Id, int Level);

public record Spells(ConcurrentBag<Spell> Items);
