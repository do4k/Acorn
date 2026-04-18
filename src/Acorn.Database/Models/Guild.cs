using System.ComponentModel.DataAnnotations;

namespace Acorn.Database.Models;

public class Guild
{
    [Key] public string Tag { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Ranks { get; set; } = string.Empty;
    public int Bank { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GuildMember> Members { get; set; } = new List<GuildMember>();
}
