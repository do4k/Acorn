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

public static class EcfExtension
{
    public static EcfRecord? GetClass(this Ecf ecf, int id)
    {
        return id > ecf.Classes.Count ? null : ecf.Classes[id];
    }
}

public static class EnfExtension
{
    public static EnfRecord? GetNpc(this Enf enf, int id)
    {
        return id > enf.Npcs.Count ? null : enf.Npcs[id];
    }
}

public static class EifExtension
{
    public static EifRecord? GetItem(this Eif eif, int id)
    {
        return id > eif.Items.Count ? null : eif.Items[id];
    }
}

public static class EsfExtension
{
    public static EsfRecord? GetSkill(this Esf esf, int id)
    {
        return id > esf.Skills.Count ? null : esf.Skills[id];
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