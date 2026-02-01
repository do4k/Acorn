namespace Acorn.Data;

/// <summary>
/// Repository for inn/citizenship data
/// </summary>
public interface IInnDataRepository
{
    /// <summary>
    /// Get inn data by NPC behavior ID
    /// </summary>
    InnData? GetInnByBehaviorId(int behaviorId);

    /// <summary>
    /// Get inn data by name (for finding current home)
    /// </summary>
    InnData? GetInnByName(string name);

    /// <summary>
    /// Get the default home name for new characters
    /// </summary>
    string DefaultHomeName { get; }

    /// <summary>
    /// Get all inns
    /// </summary>
    IEnumerable<InnData> GetAllInns();
}
