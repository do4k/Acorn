using System.Text.RegularExpressions;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Database.Repository;

public class DataFileRepository : IDataFileRepository
{
    private readonly string _ecfFile = "Data/Pub/dat001.ecf";
    private readonly string _eifFile = "Data/Pub/dat001.eif";
    private readonly string _enfFile = "Data/Pub/dtn001.enf";
    private readonly string _esfFile = "Data/Pub/dsl001.esf";

    public DataFileRepository()
    {
        if (File.Exists(_ecfFile))
        {
            Ecf.Deserialize(new EoReader(File.ReadAllBytes(_ecfFile)));
        }

        if (File.Exists(_eifFile))
        {
            Eif.Deserialize(new EoReader(File.ReadAllBytes(_eifFile)));
        }

        if (File.Exists(_enfFile))
        {
            Enf.Deserialize(new EoReader(File.ReadAllBytes(_enfFile)));
        }

        if (File.Exists(_esfFile))
        {
            Esf.Deserialize(new EoReader(File.ReadAllBytes(_esfFile)));
        }

        if (Directory.Exists("Data/Maps/"))
        {
            Maps = Directory.GetFiles("Data/Maps/").ToList().Where(f => Regex.IsMatch(f, @"\d+\.emf")).Select(mapFile =>
            {
                var emf = new Emf();
                emf.Deserialize(new EoReader(File.ReadAllBytes(mapFile)));
                var id = int.Parse(new FileInfo(mapFile).Name.Split('.')[0]);
                return new MapWithId(id, emf);
            }).ToList();
        }
        else
        {
            Maps = Array.Empty<MapWithId>();
        }
    }

    public Ecf Ecf { get; } = new();
    public Eif Eif { get; } = new();
    public Enf Enf { get; } = new();
    public Esf Esf { get; } = new();
    public IEnumerable<MapWithId> Maps { get; }
}

public static class EsfExtension
{
    public static EsfRecord? GetSkill(this Esf esf, int id)
    {
        // Skill IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= esf.Skills.Count ? null : esf.Skills[index];
    }

    /// <summary>
    /// Find skills by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EsfRecord Skill, int Id)> FindByName(this Esf esf, string name)
    {
        return esf.Skills
            .Select((skill, index) => (skill, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.skill.Name) &&
                        x.skill.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Search skills by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EsfRecord Skill, int Id)> SearchByName(this Esf esf, string name)
    {
        return esf.Skills
            .Select((skill, index) => (skill, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.skill.Name) &&
                        x.skill.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public static class EcfExtension
{
    public static EcfRecord? GetClass(this Ecf ecf, int id)
    {
        // Class IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= ecf.Classes.Count ? null : ecf.Classes[index];
    }

    /// <summary>
    /// Find classes by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EcfRecord Class, int Id)> FindByName(this Ecf ecf, string name)
    {
        return ecf.Classes
            .Select((cls, index) => (cls, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.cls.Name) &&
                        x.cls.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Search classes by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EcfRecord Class, int Id)> SearchByName(this Ecf ecf, string name)
    {
        return ecf.Classes
            .Select((cls, index) => (cls, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.cls.Name) &&
                        x.cls.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public static class EnfExtension
{
    public static EnfRecord? GetNpc(this Enf enf, int id)
    {
        // NPC IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= enf.Npcs.Count ? null : enf.Npcs[index];
    }

    /// <summary>
    /// Find NPCs by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EnfRecord Npc, int Id)> FindByName(this Enf enf, string name)
    {
        return enf.Npcs
            .Select((npc, index) => (npc, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.npc.Name) &&
                        x.npc.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Search NPCs by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EnfRecord Npc, int Id)> SearchByName(this Enf enf, string name)
    {
        return enf.Npcs
            .Select((npc, index) => (npc, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.npc.Name) &&
                        x.npc.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public static class EifExtension
{
    public static EifRecord? GetItem(this Eif eif, int id)
    {
        // Item IDs are 1-indexed, array is 0-indexed
        var index = id - 1;
        return index < 0 || index >= eif.Items.Count ? null : eif.Items[index];
    }

    /// <summary>
    /// Find items by exact name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EifRecord Item, int Id)> FindByName(this Eif eif, string name)
    {
        return eif.Items
            .Select((item, index) => (item, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.item.Name) &&
                        x.item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Search items by partial name match (case-insensitive).
    /// </summary>
    public static IReadOnlyList<(EifRecord Item, int Id)> SearchByName(this Eif eif, string name)
    {
        return eif.Items
            .Select((item, index) => (item, id: index + 1))
            .Where(x => !string.IsNullOrEmpty(x.item.Name) &&
                        x.item.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public record MapWithId(int Id, Emf Map);

public interface IDataFileRepository
{
    Ecf Ecf { get; }
    Eif Eif { get; }
    Enf Enf { get; }
    Esf Esf { get; }
    IEnumerable<MapWithId> Maps { get; }
}