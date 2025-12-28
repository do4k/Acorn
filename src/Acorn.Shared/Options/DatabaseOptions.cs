namespace Acorn.Shared.Options;

public class DatabaseOptions
{
    public string? Engine { get; set; } = "sqlite";
    public string? ConnectionString { get; set; }
}

