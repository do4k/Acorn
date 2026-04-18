using System.ComponentModel.DataAnnotations;

namespace Acorn.Database.Models;

/// <summary>
///     Tracks a character's quest progress, including current state and completion history.
/// </summary>
public class QuestProgress
{
    [Key]
    public int Id { get; set; }

    public required string CharacterName { get; set; }
    public int QuestId { get; set; }
    public int State { get; set; }
    public string NpcKillsJson { get; set; } = "{}";
    public int PlayerKills { get; set; }
    public DateTime? DoneAt { get; set; }
    public int Completions { get; set; }
}
