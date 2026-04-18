namespace Acorn.Data;

/// <summary>
/// Repository for skill master NPC data (learnable skills)
/// </summary>
public interface ISkillMasterDataRepository
{
    /// <summary>
    /// Get skill master data by NPC behavior ID
    /// </summary>
    SkillMasterData? GetByBehaviorId(int behaviorId);

    /// <summary>
    /// Get all skill masters
    /// </summary>
    IEnumerable<SkillMasterData> GetAll();
}
