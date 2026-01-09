using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acorn.Database.Models;

public class CharacterItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required][MaxLength(16)] public required string CharacterName { get; set; }

    [Required] public int ItemId { get; set; }

    [Required] public int Amount { get; set; }

    /// <summary>
    ///     0 = Inventory, 1 = Bank
    /// </summary>
    [Required]
    public int Slot { get; set; }
}