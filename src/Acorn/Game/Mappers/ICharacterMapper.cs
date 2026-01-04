using Acorn.Database.Models;

namespace Acorn.Game.Mappers;

/// <summary>
///     Maps between game models and database models.
/// </summary>
public interface ICharacterMapper
{
    /// <summary>
    ///     Converts a game Character to a database Character model.
    /// </summary>
    Character ToDatabase(Models.Character character);

    /// <summary>
    ///     Converts a database Character to a game Character model.
    /// </summary>
    Models.Character FromDatabase(Character dbCharacter);
}