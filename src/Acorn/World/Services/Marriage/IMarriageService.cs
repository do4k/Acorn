using Acorn.Net;
using Acorn.World.Map;

namespace Acorn.World.Services.Marriage;

/// <summary>
///     Service responsible for managing wedding ceremonies and divorce operations.
/// </summary>
public interface IMarriageService
{
    /// <summary>
    ///     Open the priest NPC dialog for a player. Validates eligibility.
    /// </summary>
    Task OpenPriestAsync(PlayerState player, int npcIndex);

    /// <summary>
    ///     Request a wedding ceremony. Validates both players and notifies the partner.
    /// </summary>
    Task RequestWeddingAsync(PlayerState player, string partnerName);

    /// <summary>
    ///     Partner accepts the wedding request.
    /// </summary>
    Task AcceptWeddingRequestAsync(PlayerState player);

    /// <summary>
    ///     Player says "I do" during the ceremony.
    /// </summary>
    Task SayIDoAsync(PlayerState player);

    /// <summary>
    ///     Open the law office NPC dialog for a player.
    /// </summary>
    Task OpenLawAsync(PlayerState player, int npcIndex);

    /// <summary>
    ///     Request marriage approval at the law office (set fiance).
    /// </summary>
    Task RequestMarriageApprovalAsync(PlayerState player, string partnerName);

    /// <summary>
    ///     Request divorce at the law office.
    /// </summary>
    Task RequestDivorceAsync(PlayerState player, string partnerName);

    /// <summary>
    ///     Process the wedding ceremony tick for a map. Called each map tick.
    /// </summary>
    Task ProcessWeddingTickAsync(MapState map);
}
