namespace Acorn.World.Npc;

public record GlobalMessage(
    Guid Id,
    string Message,
    string Author,
    DateTime CreatedAt
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