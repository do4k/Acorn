using Acorn.Game.Mappers;
using Acorn.Game.Models;
using FluentAssertions;
using Xunit;
using DatabaseCharacter = Acorn.Database.Models.Character;

namespace Acorn.Tests.Game.Mappers;

public class CharacterMapperTests
{
    [Fact]
    public void FromDatabaseModel_ShouldMapKnownSpells()
    {
        var dbCharacter = new DatabaseCharacter
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            Spells = new List<Acorn.Database.Models.CharacterSpell>
            {
                new() { CharacterName = "TestCharacter", SpellId = 3, Level = 2 },
                new() { CharacterName = "TestCharacter", SpellId = 7, Level = 5 }
            }
        };

        var character = CharacterMapper.FromDatabaseModel(dbCharacter);

        character.Spells.Items.Should().BeEquivalentTo(new[]
        {
            new Spell(3, 2),
            new Spell(7, 5)
        });
    }
}
