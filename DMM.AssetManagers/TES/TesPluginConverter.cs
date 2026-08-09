using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace DMM.AssetManagers.TES;

public enum TesMasterSize
{
    Small,
    Medium,
    Full
}

public enum TesPluginOutputType
{
    Automatic,
    Esp,
    Esm
}

public sealed record TesPluginConversionResult(
    string InputPath,
    string OutputPath,
    string? BackupPath,
    uint RecordCount,
    TesMasterSize? MasterSize);

/// <summary>Converts Starfield ESP/ESM plugins by updating only the TES4 header flags.</summary>
public sealed class TesPluginConverter
{
    // These are inclusive maxima. Using 2^n - 1 explicitly avoids treating the
    // first record beyond the available ID range as part of the smaller layout.
    public const uint SmallRecordLimit = 4095;
    public const uint MediumRecordLimit = 65535;

    private const uint MasterFlag = 0x00000001;
    // Starfield xEdit defines 0x100 as Small, 0x200 as Update, and 0x400 as Medium.
    private const uint SmallMasterFlag = 0x00000100;
    private const uint MediumMasterFlag = 0x00000400;
    private const uint EspClearedFlags = MasterFlag | SmallMasterFlag | MediumMasterFlag;
    private const int RecordHeaderSize = 24;
    private readonly TimeProvider _timeProvider;

    public TesPluginConverter(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TesPluginConversionResult Convert(
        string inputPath,
        TesMasterSize? requestedMasterSize = null,
        TesPluginOutputType outputType = TesPluginOutputType.Automatic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        string fullInputPath = Path.GetFullPath(inputPath);
        string extension = Path.GetExtension(fullInputPath);
        bool inputIsEsm = extension.Equals(".esm", StringComparison.OrdinalIgnoreCase);
        bool inputIsEsp = extension.Equals(".esp", StringComparison.OrdinalIgnoreCase);
        if (!inputIsEsp && !inputIsEsm)
            throw new ArgumentException("The input file must have an .esp or .esm extension.", nameof(inputPath));

        // With no options, preserve drag-and-drop toggling. Supplying a size always
        // requests an ESM, including when the source is already an ESM.
        bool toEsp = outputType == TesPluginOutputType.Esp ||
                     (outputType == TesPluginOutputType.Automatic && requestedMasterSize is null && inputIsEsm);
        bool toEsm = !toEsp;
        if (toEsp && requestedMasterSize is not null)
            throw new ArgumentException("A master size cannot be combined with ESP output.", nameof(requestedMasterSize));
        if (outputType == TesPluginOutputType.Esp && inputIsEsp)
            throw new ArgumentException("ESP output requires an ESM input file.", nameof(outputType));

        byte[] plugin = File.ReadAllBytes(fullInputPath);
        uint recordCount = ReadRecordCount(plugin);
        TesMasterSize? outputSize = null;

        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(plugin.AsSpan(8, sizeof(uint)));
        // This mask is the complete ESP conversion: ESPs are non-masters and have
        // neither compact master-size bit. ESM output adds its required bits below.
        flags &= ~EspClearedFlags;

        if (toEsm)
        {
            TesMasterSize minimumSize = GetMinimumMasterSize(recordCount);
            outputSize = requestedMasterSize ?? minimumSize;
            if (outputSize < minimumSize)
            {
                throw new InvalidOperationException(
                    $"{recordCount} records require a {minimumSize.ToString().ToLowerInvariant()} master; " +
                    $"the requested {outputSize.Value.ToString().ToLowerInvariant()} master is too small.");
            }

            flags |= MasterFlag;
            flags |= outputSize switch
            {
                TesMasterSize.Small => SmallMasterFlag,
                TesMasterSize.Medium => MediumMasterFlag,
                _ => 0
            };
        }
        BinaryPrimitives.WriteUInt32LittleEndian(plugin.AsSpan(8, sizeof(uint)), flags);
        string outputPath = Path.ChangeExtension(fullInputPath, toEsp ? ".esp" : ".esm");
        string? backupPath = BackupExistingOutput(outputPath);
        File.WriteAllBytes(outputPath, plugin);
        return new TesPluginConversionResult(fullInputPath, outputPath, backupPath, recordCount, outputSize);
    }

    public static TesMasterSize GetMinimumMasterSize(uint recordCount) => recordCount switch
    {
        <= SmallRecordLimit => TesMasterSize.Small,
        <= MediumRecordLimit => TesMasterSize.Medium,
        _ => TesMasterSize.Full
    };

    private string? BackupExistingOutput(string outputPath)
    {
        if (!File.Exists(outputPath))
            return null;

        string timestamp = _timeProvider.GetLocalNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string backupPath = $"{outputPath}.bak.{timestamp}";
        File.Move(outputPath, backupPath);
        return backupPath;
    }

    private static uint ReadRecordCount(byte[] plugin)
    {
        if (plugin.Length < RecordHeaderSize || Encoding.ASCII.GetString(plugin, 0, 4) != "TES4")
            throw new InvalidDataException("The file does not begin with a valid TES4 record header.");

        uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(plugin.AsSpan(4, sizeof(uint)));
        if (dataSize > plugin.Length - RecordHeaderSize)
            throw new InvalidDataException("The TES4 header payload is truncated.");

        int position = RecordHeaderSize;
        int end = checked(position + (int)dataSize);
        while (position + 6 <= end)
        {
            string type = Encoding.ASCII.GetString(plugin, position, 4);
            ushort size = BinaryPrimitives.ReadUInt16LittleEndian(plugin.AsSpan(position + 4, sizeof(ushort)));
            position += 6;
            if (position + size > end)
                throw new InvalidDataException("The TES4 header contains a truncated subrecord.");

            // HEDR: version (float), number of records (uint32), next object ID (uint32).
            if (type == "HEDR")
            {
                if (size < 12)
                    throw new InvalidDataException("The TES4 HEDR subrecord is too short.");
                return BinaryPrimitives.ReadUInt32LittleEndian(plugin.AsSpan(position + 4, sizeof(uint)));
            }

            position += size;
        }

        throw new InvalidDataException("The TES4 header does not contain a HEDR subrecord.");
    }
}
