namespace Acorn.Data;

public abstract record QuestArg
{
    public record IntArg(int Value) : QuestArg;
    public record StrArg(string Value) : QuestArg;

    public int AsInt() => this is IntArg i ? i.Value : 0;
    public string AsStr() => this is StrArg s ? s.Value : "";
}
