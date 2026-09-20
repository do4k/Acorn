namespace Acorn.World;

public record GlobalMessage(
    Guid Id,
    string Message,
    string Author,
    DateTime CreatedAt,
    long Sequence = 0
)
{
    public static GlobalMessage Welcome()
    {
        return new GlobalMessage(
            Guid.NewGuid(),
            "Welcome to Acorn! Please be respectful.",
            "Server",
            DateTime.UtcNow
        );
    }
}