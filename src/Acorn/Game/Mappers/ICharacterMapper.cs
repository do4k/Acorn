using Acorn.Database.Models;
using Acorn.Domain.Models;
using DatabaseCharacter = Acorn.Database.Models.Character;
using GameCharacter = Acorn.Domain.Models.Character;

namespace Acorn.Game.Mappers;

/// <summary>
///     Maps between game models and database models.
/// </summary>
public interface ICharacterMapper
{
    /// <summary>
    ///     Converts a game Character to a database Character model.
    /// </summary>
    DatabaseCharacter ToDatabase(GameCharacter character);

    /// <summary>
    ///     Converts a database Character to a game Character model.
    /// </summary>
    GameCharacter FromDatabase(DatabaseCharacter dbCharacter);
}