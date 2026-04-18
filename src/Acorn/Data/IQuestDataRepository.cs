namespace Acorn.Data;

public interface IQuestDataRepository
{
    IReadOnlyDictionary<int, QuestData> Quests { get; }
    QuestData? GetQuest(int questId);
}
