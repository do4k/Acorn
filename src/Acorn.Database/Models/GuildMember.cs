using System.ComponentModel.DataAnnotations;

namespace Acorn.Database.Models;

public class GuildMember
{
    [Key] public int Id { get; set; }

    public string CharacterName { get; set; } = string.Empty;
    public string GuildTag { get; set; } = string.Empty;
    public int RankIndex { get; set; }

    public Guild? Guild { get; set; }
}
