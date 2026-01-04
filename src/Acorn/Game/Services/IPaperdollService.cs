using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Game.Services;

public interface IPaperdollService
{
    /// <summary>
    /// Converts paperdoll to EquipmentWelcome (uses item IDs).
    /// Used when a player enters the game.
    /// </summary>
    EquipmentWelcome ToEquipmentWelcome(EquipmentPaperdoll paperdoll);

    /// <summary>
    /// Converts paperdoll to EquipmentChange (uses graphic IDs from spec1).
    /// Used when broadcasting equipment changes to other players on the map.
    /// </summary>
    EquipmentChange ToEquipmentChange(EquipmentPaperdoll paperdoll);

    /// <summary>
    /// Converts character to EquipmentCharacterSelect (uses item IDs).
    /// Used for character selection screen.
    /// </summary>
    EquipmentCharacterSelect ToEquipmentCharacterSelect(Character character);

    /// <summary>
    /// Converts equipment paperdoll item IDs to graphic IDs for paperdoll display.
    /// Used for paperdoll window display.
    /// </summary>
    EquipmentPaperdoll ToEquipmentPaperdoll(EquipmentPaperdoll paperdoll);
}
