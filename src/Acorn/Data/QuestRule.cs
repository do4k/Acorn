namespace Acorn.Data;

public record QuestRule(string Name, List<QuestArg> Args, string Goto);
