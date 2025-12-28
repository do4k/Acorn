namespace Acorn.Shared.Models.Online;

/// <summary>
/// Character equipment/paperdoll.
/// </summary>
public record EquipmentRecord
{
    public int Weapon { get; init; }
    public int Shield { get; init; }
    public int Armor { get; init; }
    public int Hat { get; init; }
    public int Boots { get; init; }
    public int Gloves { get; init; }
    public int Belt { get; init; }
    public int Necklace { get; init; }
    public int Ring1 { get; init; }
    public int Ring2 { get; init; }
    public int Armlet1 { get; init; }
    public int Armlet2 { get; init; }
    public int Bracer1 { get; init; }
    public int Bracer2 { get; init; }
}

