using Acorn.Domain.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Game.Services;

public interface IPaperdollService
{
    /// <summary>
    ///     Converts paperdoll to EquipmentWelcome (uses item IDs).
    ///     Used when a player enters the game.
    /// </summary>
    EquipmentWelcome ToEquipmentWelcome(EquipmentPaperdoll paperdoll);

    /// <summary>
    ///     Converts paperdoll to EquipmentChange (uses graphic IDs from spec1).
    ///     Used when broadcasting equipment changes to other players on the map.
    /// </summary>
    EquipmentChange ToEquipmentChange(EquipmentPaperdoll paperdoll);

    /// <summary>
    ///     Converts character to EquipmentCharacterSelect (uses item IDs).
    ///     Used for character selection screen.
    /// </summary>
    EquipmentCharacterSelect ToEquipmentCharacterSelect(Character character);

    /// <summary>
    ///     Gets the graphic ID (spec1) for an item ID.
    ///     Returns 0 if item not found or itemId is 0.
    /// </summary>
    int GetGraphicId(int itemId);
}