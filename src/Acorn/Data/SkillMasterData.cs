namespace Acorn.Data;

public record SkillMasterData(
    int BehaviorId,
    string Name,
    int MinLevel,
    int MaxLevel,
    int ClassRequirement,
    List<SkillMasterSkill> Skills);
