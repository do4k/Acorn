namespace Acorn.Data;

/// <summary>
///     Represents a parsed quest file (EQF format).
/// </summary>
public record QuestData(int Id, string Name, int Version, List<QuestState> States);

public record QuestState(string Name, string Description, List<QuestAction> Actions, List<QuestRule> Rules);

public record QuestAction(string Name, List<QuestArg> Args);

public record QuestRule(string Name, List<QuestArg> Args, string Goto);

public abstract record QuestArg
{
    public record IntArg(int Value) : QuestArg;
    public record StrArg(string Value) : QuestArg;

    public int AsInt() => this is IntArg i ? i.Value : 0;
    public string AsStr() => this is StrArg s ? s.Value : "";
}
