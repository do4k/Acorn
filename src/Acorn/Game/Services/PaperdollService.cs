using Acorn.Database.Repository;
using Acorn.Domain.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Game.Services;

public class PaperdollService : IPaperdollService
{
    private readonly IDataFileRepository _dataFiles;
    private readonly ILogger<PaperdollService> _logger;

    public PaperdollService(IDataFileRepository dataFiles, ILogger<PaperdollService> logger)
    {
        _dataFiles = dataFiles;
        _logger = logger;
    }

    public EquipmentWelcome ToEquipmentWelcome(EquipmentPaperdoll paperdoll)
    {
        return new EquipmentWelcome
        {
            Accessory = paperdoll.Accessory,
            Armlet = paperdoll.Armlet,
            Armor = paperdoll.Armor,
            Belt = paperdoll.Belt,
            Boots = paperdoll.Boots,
            Bracer = paperdoll.Bracer,
            Gloves = paperdoll.Gloves,
            Hat = paperdoll.Hat,
            Necklace = paperdoll.Necklace,
            Ring = paperdoll.Ring,
            Shield = paperdoll.Shield,
            Weapon = paperdoll.Weapon
        };
    }

    public EquipmentChange ToEquipmentChange(EquipmentPaperdoll paperdoll)
    {
        return new EquipmentChange
        {
            Armor = GetGraphicId(paperdoll.Armor),
            Boots = GetGraphicId(paperdoll.Boots),
            Hat = GetGraphicId(paperdoll.Hat),
            Shield = GetGraphicId(paperdoll.Shield),
            Weapon = GetGraphicId(paperdoll.Weapon)
        };
    }

    public EquipmentCharacterSelect ToEquipmentCharacterSelect(Character character)
    {
        return new EquipmentCharacterSelect
        {
            Armor = GetGraphicId(character.Paperdoll.Armor),
            Boots = GetGraphicId(character.Paperdoll.Boots),
            Weapon = GetGraphicId(character.Paperdoll.Weapon),
            Shield = GetGraphicId(character.Paperdoll.Shield),
            Hat = GetGraphicId(character.Paperdoll.Hat)
        };
    }

    public int GetGraphicId(int itemId)
    {
        if (itemId == 0)
        {
            return 0;
        }

        var item = _dataFiles.Eif.GetItem(itemId);
        var graphicId = item?.Spec1 ?? 0;

        _logger.LogDebug("GetGraphicId: ItemId={ItemId} -> GraphicId={GraphicId} (Item={ItemName})",
            itemId, graphicId, item?.Name ?? "NOT FOUND");

        return graphicId;
    }
}