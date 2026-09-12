namespace Acorn.Data;

/// <summary>
///     Represents a parsed quest file (EQF format).
/// </summary>
public record QuestData(int Id, string Name, int Version, List<QuestState> States);
