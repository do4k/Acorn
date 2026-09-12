namespace Acorn.Data;

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
