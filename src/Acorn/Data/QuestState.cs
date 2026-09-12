namespace Acorn.Data;

public record QuestState(string Name, string Description, List<QuestAction> Actions, List<QuestRule> Rules);
