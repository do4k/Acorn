namespace Acorn.Data;

public record SkillMasterData(
    int BehaviorId,
    string Name,
    int MinLevel,
    int MaxLevel,
    int ClassRequirement,
    List<SkillMasterSkill> Skills);

public record SkillMasterSkill(
    int SkillId,
    int LevelRequirement,
    int ClassRequirement,
    int Price,
    List<int> SkillRequirements,
    int StrRequirement,
    int IntRequirement,
    int WisRequirement,
    int AgiRequirement,
    int ConRequirement,
    int ChaRequirement);
