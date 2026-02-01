namespace Acorn.Data;

/// <summary>
/// Represents a question for inn citizenship
/// </summary>
public record InnQuestion(
    string Question,
    string Answer
);

/// <summary>
/// Represents an inn/citizenship location configuration
/// </summary>
public record InnData(
    int BehaviorId,
    string Name,
    int SpawnMap,
    int SpawnX,
    int SpawnY,
    int SleepMap,
    int SleepX,
    int SleepY,
    bool AlternateSpawnEnabled,
    int AlternateSpawnMap,
    int AlternateSpawnX,
    int AlternateSpawnY,
    List<InnQuestion> Questions
);
