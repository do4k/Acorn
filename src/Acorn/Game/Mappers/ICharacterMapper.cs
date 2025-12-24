using Acorn.Game.Models;

namespace Acorn.Game.Mappers;

/// <summary>
/// Maps between game models and database models.
/// </summary>
public interface ICharacterMapper
{
    /// <summary>
    /// Converts a game Character to a database Character model.
    /// </summary>
    Database.Models.Character ToDatabase(Character character);

    /// <summary>
    /// Converts a database Character to a game Character model.
    /// </summary>
    Character FromDatabase(Database.Models.Character dbCharacter);
}
