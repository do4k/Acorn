using System.IO.Hashing;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<DataFileRepository> _logger;

    public DataFileRepository(ILogger<DataFileRepository> logger)
    {
        _logger = logger;

        if (File.Exists(_ecfFile))
        {
            Ecf.Deserialize(new EoReader(File.ReadAllBytes(_ecfFile)));
            RecalculateRid(Ecf);
        }

        if (File.Exists(_eifFile))
        {
            Eif.Deserialize(new EoReader(File.ReadAllBytes(_eifFile)));
            RecalculateRid(Eif);
        }

        if (File.Exists(_enfFile))
        {
            Enf.Deserialize(new EoReader(File.ReadAllBytes(_enfFile)));
            RecalculateRid(Enf);
        }

        if (File.Exists(_esfFile))
        {
            Esf.Deserialize(new EoReader(File.ReadAllBytes(_esfFile)));
            RecalculateRid(Esf);
        }

        if (Directory.Exists("Data/Maps/"))
        {
            Maps = Directory.GetFiles("Data/Maps/").ToList().Where(f => Regex.IsMatch(f, @"\d+\.emf")).Select(mapFile =>
            {
                var emf = new Emf();
                emf.Deserialize(new EoReader(File.ReadAllBytes(mapFile)));
                RecalculateRid(emf);
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

    /// <summary>
    ///     Manually encode a uint32 value as 4 EO-encoded bytes.
    ///     This is needed because EoWriter.AddInt validates that values are positive,
    ///     but CRC32 can produce values > int.MaxValue.
    /// </summary>
    private static byte[] EncodeEoInt(uint value)
    {
        var bytes = new byte[4];
        bytes[0] = (byte)(value % 253 + 1);
        bytes[1] = (byte)(value / 253 % 253 + 1);
        bytes[2] = (byte)(value / 64009 % 253 + 1);
        bytes[3] = (byte)(value / 16194277 % 253 + 1);
        return bytes;
    }

    /// <summary>
    ///     Recalculates the RID (CRC32 checksum) for an EIF pub file.
    ///     starting at byte 7 (after the header).
    /// </summary>
    private void RecalculateRid(Eif eif)
    {
        var oldRid = eif.Rid.ToArray();

        var writer = new EoWriter();
        eif.Serialize(writer);
        var bytes = writer.ToByteArray();

        var checksum = Crc32.Hash(bytes.AsSpan(7));
        var checksumValue = BitConverter.ToUInt32(checksum);

        // Split the uint32 into two shorts (lower 16 bits, upper 16 bits)
        var encodedWriter = new EoWriter();
        encodedWriter.AddShort((int)(checksumValue & 0xFFFF)); // Lower 16 bits
        encodedWriter.AddShort((int)((checksumValue >> 16) & 0xFFFF)); // Upper 16 bits
        var encoded = encodedWriter.ToByteArray();

        var ridReader1 = new EoReader(encoded.AsSpan(0, 2).ToArray());
        var ridReader2 = new EoReader(encoded.AsSpan(2, 2).ToArray());

        eif.Rid = [ridReader1.GetShort(), ridReader2.GetShort()];

        _logger.LogInformation("EIF RID: Old={OldRid}, Calculated={NewRid}, CRC32={Crc32}",
            string.Join(",", oldRid), string.Join(",", eif.Rid), checksumValue);
    }

    /// <summary>
    ///     Recalculates the RID (CRC32 checksum) for an ENF pub file.
    /// </summary>
    private void RecalculateRid(Enf enf)
    {
        var writer = new EoWriter();
        enf.Serialize(writer);
        var bytes = writer.ToByteArray();

        var checksum = Crc32.Hash(bytes.AsSpan(7));
        var checksumValue = BitConverter.ToUInt32(checksum);

        var encoded = EncodeEoInt(checksumValue);

        var ridReader1 = new EoReader(encoded.AsSpan(0, 2).ToArray());
        var ridReader2 = new EoReader(encoded.AsSpan(2, 2).ToArray());

        enf.Rid = [ridReader1.GetChar(), ridReader2.GetChar()];
    }

    /// <summary>
    ///     Recalculates the RID (CRC32 checksum) for an ESF pub file.
    /// </summary>
    private void RecalculateRid(Esf esf)
    {
        var writer = new EoWriter();
        esf.Serialize(writer);
        var bytes = writer.ToByteArray();

        var checksum = Crc32.Hash(bytes.AsSpan(7));
        var checksumValue = BitConverter.ToUInt32(checksum);

        var encoded = EncodeEoInt(checksumValue);

        var ridReader1 = new EoReader(encoded.AsSpan(0, 2).ToArray());
        var ridReader2 = new EoReader(encoded.AsSpan(2, 2).ToArray());

        esf.Rid = [ridReader1.GetChar(), ridReader2.GetChar()];
    }

    /// <summary>
    ///     Recalculates the RID (CRC32 checksum) for an ECF pub file.
    /// </summary>
    private void RecalculateRid(Ecf ecf)
    {
        var writer = new EoWriter();
        ecf.Serialize(writer);
        var bytes = writer.ToByteArray();

        var checksum = Crc32.Hash(bytes.AsSpan(7));
        var checksumValue = BitConverter.ToUInt32(checksum);

        var encoded = EncodeEoInt(checksumValue);

        var ridReader1 = new EoReader(encoded.AsSpan(0, 2).ToArray());
        var ridReader2 = new EoReader(encoded.AsSpan(2, 2).ToArray());

        ecf.Rid = [ridReader1.GetChar(), ridReader2.GetChar()];
    }

    /// <summary>
    ///     Recalculates the RID (CRC32 checksum) for an EMF map file.
    /// </summary>
    private void RecalculateRid(Emf emf)
    {
        var writer = new EoWriter();
        emf.Serialize(writer);
        var bytes = writer.ToByteArray();

        var checksum = Crc32.Hash(bytes.AsSpan(7));
        var checksumValue = BitConverter.ToUInt32(checksum);

        var encoded = EncodeEoInt(checksumValue);

        var ridReader1 = new EoReader(encoded.AsSpan(0, 2).ToArray());
        var ridReader2 = new EoReader(encoded.AsSpan(2, 2).ToArray());

        emf.Rid = [ridReader1.GetChar(), ridReader2.GetChar()];
    }
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
    ///     Find skills by exact name match (case-insensitive).
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
    ///     Search skills by partial name match (case-insensitive).
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
    ///     Find classes by exact name match (case-insensitive).
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
    ///     Search classes by partial name match (case-insensitive).
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
    ///     Find NPCs by exact name match (case-insensitive).
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
    ///     Search NPCs by partial name match (case-insensitive).
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
    ///     Find items by exact name match (case-insensitive).
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
    ///     Search items by partial name match (case-insensitive).
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
