using Acorn.Database.Models;
using Acorn.Database.Repository;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn;

public class FormulaService
{
    private readonly IDataFileRepository _dataFileRepository;

    public FormulaService(IDataFileRepository dataFileRepository)
    {
        _dataFileRepository = dataFileRepository;
    }

    public int CalculateDamage(Game.Models.Character character, EnfRecord npcData)
    {
        // Example formula for calculating damage
        // This is a placeholder and should be replaced with the actual game logic
        var @class = _dataFileRepository.Ecf.GetClass(character.Class);
        if (@class is null)
        {
            return 0;
        }

        var baseDamage = @class.StatGroup switch
        {
            2 => @class.Str * 2 + character.Level,
            _ => @class.Str + character.Level,
        };

        var npcDefense = npcData.Armor;

        // Calculate final damage considering Npc defense
        var finalDamage = baseDamage - (npcDefense / 2);

        // Ensure damage is not negative
        return Math.Max(finalDamage, 0);

    }
}