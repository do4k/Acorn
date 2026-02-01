using System.ComponentModel.DataAnnotations;

namespace Acorn.Database.Models;

public class BoardPost
{
    [Key]
    public int Id { get; set; }

    public int BoardId { get; set; }

    [Required]
    [MaxLength(16)]
    public required string CharacterName { get; set; }

    [Required]
    [MaxLength(64)]
    public required string Subject { get; set; }

    [Required]
    [MaxLength(2048)]
    public required string Body { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
