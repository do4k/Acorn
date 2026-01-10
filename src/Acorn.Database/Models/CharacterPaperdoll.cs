using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Acorn.Database.Models;

public class CharacterPaperdoll
{
    [Key][MaxLength(16)] public required string CharacterName { get; set; }

    public int Hat { get; set; }
    public int Necklace { get; set; }
    public int Armor { get; set; }
    public int Belt { get; set; }
    public int Boots { get; set; }
    public int Gloves { get; set; }
    public int Weapon { get; set; }
    public int Shield { get; set; }
    public int Accessory { get; set; }
    public int Ring1 { get; set; }
    public int Ring2 { get; set; }
    public int Bracer1 { get; set; }
    public int Bracer2 { get; set; }
    public int Armlet1 { get; set; }
    public int Armlet2 { get; set; }

    // Navigation property - ignored during JSON serialization to prevent cycles
    [JsonIgnore]
    public Character? Character { get; set; }
}
