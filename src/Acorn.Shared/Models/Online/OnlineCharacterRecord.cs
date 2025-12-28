namespace Acorn.Shared.Models.Online;

/// <summary>
/// Cached online character for API consumption.
/// </summary>
public record OnlineCharacterRecord
{
    public int SessionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string GuildName { get; init; } = string.Empty;
    public string GuildRank { get; init; } = string.Empty;
    public int Level { get; init; }
    public int Class { get; init; }
    public string Gender { get; init; } = string.Empty;
    public string Admin { get; init; } = string.Empty;
    public int MapId { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public string Direction { get; init; } = string.Empty;
    
    // Stats
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Tp { get; init; }
    public int MaxTp { get; init; }
    public int Exp { get; init; }
    
    // Equipment
    public EquipmentRecord Equipment { get; init; } = new();
}

