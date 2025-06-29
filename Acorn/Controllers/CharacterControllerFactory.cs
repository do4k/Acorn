using Acorn.Database.Models;
using Acorn.Database.Repository;

namespace Acorn.Controllers;

public interface ICharacterControllerFactory
{
    CharacterController Create(Character character);
}

public class CharacterControllerFactory(IDbRepository<Character> repository) : ICharacterControllerFactory
{
    public CharacterController Create(Character character)
    {
        return new CharacterController(character, repository);
    }
}