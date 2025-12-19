using System.ComponentModel.DataAnnotations;

namespace Acorn.Database.Models;

public class Guild
{
    [Key]
    public string? Tag { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Ranks { get; set; }
    public int Bank { get; set; }
}