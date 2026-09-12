using System.IO.Hashing;
using System.Text.RegularExpressions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Database.Repository;

public class DataFileRepository : IDataFileRepository
{
    private readonly string _ecfFile;
    private readonly string _eifFile;
    private readonly string _enfFile;
    private readonly string _esfFile;
    private readonly string _mapsPath;
    private readonly ILogger<DataFileRepository> _logger;

    public DataFileRepository(IOptions<DataOptions> dataOptions, ILogger<DataFileRepository> logger)
    {
        _logger = logger;
        var options = dataOptions.Value;
        _ecfFile = options.EcfFile;
        _eifFile = options.EifFile;
        _enfFile = options.EnfFile;
        _esfFile = options.EsfFile;
        _mapsPath = options.MapsPath;

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

        if (Directory.Exists(_mapsPath))
        {
            Maps = Directory.GetFiles(_mapsPath).ToList().Where(f => Regex.IsMatch(f, @"\d+\.emf")).Select(mapFile =>
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
