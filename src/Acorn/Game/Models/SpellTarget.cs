namespace Acorn.Game.Models;

public enum SpellTarget
{
    Player,
    Group,
    OtherPlayer,
    Npc
}

public record SpellTargetDetails
{
    public static SpellTargetDetails Player() => new() { Target = SpellTarget.Player };
    public static SpellTargetDetails Group() => new() { Target = SpellTarget.Group };
    public static SpellTargetDetails OtherPlayer(int victimId) => new() { Target = SpellTarget.OtherPlayer, VictimId = victimId };
    public static SpellTargetDetails Npc(int victimId) => new() { Target = SpellTarget.Npc, VictimId = victimId };

    public required SpellTarget Target { get; init; }
    public int? VictimId { get; init; }
}
