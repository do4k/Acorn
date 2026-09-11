using System.ComponentModel.DataAnnotations;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Database.Models;

public class BoardPost
{
    [Key]
    public int Id { get; set; }

    public int BoardId { get; set; }

    [Required]
    [MaxLength(16)]
    public required string CharacterName { get; set; }

    /// <summary>Admin level of the author at the time the post was created.</summary>
    public AdminLevel AuthorAdmin { get; set; }

    [Required]
    [MaxLength(64)]
    public required string Subject { get; set; }

    [Required]
    [MaxLength(2048)]
    public required string Body { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
