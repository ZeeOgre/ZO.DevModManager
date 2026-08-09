using System.Text;
using DMM.AssetManagers.TES;

namespace DMM.Tests.Unit;

public sealed class TesFileTests
{
    [Fact]
    public void RecordLimits_Are_Inclusive_Maximums()
    {
        Assert.Equal(4095u, TesPluginConverter.SmallRecordLimit);
        Assert.Equal(65535u, TesPluginConverter.MediumRecordLimit);
    }

    [Theory]
    [InlineData(4095, TesMasterSize.Small)]
    [InlineData(4096, TesMasterSize.Medium)]
    [InlineData(65535, TesMasterSize.Medium)]
    [InlineData(65536, TesMasterSize.Full)]
    public void GetMinimumMasterSize_Uses_Record_Count_Boundaries(uint count, TesMasterSize expected)
    {
        Assert.Equal(expected, TesPluginConverter.GetMinimumMasterSize(count));
    }

    [Fact]
    public void Convert_Allows_Larger_Master_But_Blocks_Insufficient_Master()
    {
        string root = CreateTempRoot();
        try
        {
            string smallEsp = Path.Combine(root, "small.esp");
            File.WriteAllBytes(smallEsp, BuildTes4Plugin(recordCount: 100));
            var converter = new TesPluginConverter();

            TesPluginConversionResult result = converter.Convert(smallEsp, TesMasterSize.Full);

            Assert.Equal(TesMasterSize.Full, result.MasterSize);
            Assert.Equal(1u, ReadFlags(result.OutputPath) & 0x501u);

            string fullEsp = Path.Combine(root, "full.esp");
            File.WriteAllBytes(fullEsp, BuildTes4Plugin(recordCount: 65536));
            var error = Assert.Throws<InvalidOperationException>(() => converter.Convert(fullEsp, TesMasterSize.Small));
            Assert.Contains("require a full master", error.Message);
            Assert.False(File.Exists(Path.ChangeExtension(fullEsp, ".esm")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Convert_Esm_To_Esp_Clears_Master_And_Size_Flags()
    {
        string root = CreateTempRoot();
        try
        {
            string esm = Path.Combine(root, "master.esm");
            File.WriteAllBytes(esm, BuildTes4Plugin(recordCount: 12, flags: 0x501));

            TesPluginConversionResult result = new TesPluginConverter().Convert(esm);

            Assert.Null(result.MasterSize);
            Assert.Equal(0u, ReadFlags(result.OutputPath) & 0x501u);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Convert_Esm_With_Size_Produces_Requested_Esm()
    {
        string root = CreateTempRoot();
        try
        {
            string esm = Path.Combine(root, "master.esm");
            File.WriteAllBytes(esm, BuildTes4Plugin(recordCount: 12, flags: 0x101));

            TesPluginConversionResult result = new TesPluginConverter().Convert(esm, TesMasterSize.Medium);

            Assert.Equal(esm, result.OutputPath);
            Assert.Equal(TesMasterSize.Medium, result.MasterSize);
            Assert.Equal(0x401u, ReadFlags(result.OutputPath) & 0x501u);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Convert_Esm_Can_Explicitly_Produce_Non_Master_Esp()
    {
        string root = CreateTempRoot();
        try
        {
            string esm = Path.Combine(root, "master.esm");
            File.WriteAllBytes(esm, BuildTes4Plugin(recordCount: 12, flags: 0x101));

            TesPluginConversionResult result = new TesPluginConverter().Convert(
                esm,
                outputType: TesPluginOutputType.Esp);

            Assert.Equal(Path.ChangeExtension(esm, ".esp"), result.OutputPath);
            Assert.Equal(0u, ReadFlags(result.OutputPath) & 0x501u);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Convert_Esm_To_Esp_Removes_Starfield_Small_Flag_And_Preserves_Incc()
    {
        string root = CreateTempRoot();
        try
        {
            string esm = Path.Combine(root, "ck-master.esm");
            byte[] original = BuildTes4Plugin(recordCount: 12, flags: 0x101, includeIncc: true);
            File.WriteAllBytes(esm, original);

            TesPluginConversionResult result = new TesPluginConverter().Convert(esm);
            byte[] converted = File.ReadAllBytes(result.OutputPath);

            Assert.Equal(0u, ReadFlags(result.OutputPath));
            Assert.Equal(original.Length, converted.Length);
            Assert.Equal(original.AsSpan(0, 8).ToArray(), converted.AsSpan(0, 8).ToArray());
            Assert.Equal(original.AsSpan(12).ToArray(), converted.AsSpan(12).ToArray());
            Assert.Equal("INCC", Encoding.ASCII.GetString(converted, 42, 4));
            Assert.Equal(0u, BitConverter.ToUInt32(converted, 48));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Convert_Backs_Up_Existing_Output_With_Local_Timestamp()
    {
        string root = CreateTempRoot();
        try
        {
            string esp = Path.Combine(root, "example.esp");
            string esm = Path.Combine(root, "example.esm");
            byte[] previousOutput = [1, 2, 3, 4];
            File.WriteAllBytes(esp, BuildTes4Plugin(recordCount: 12));
            File.WriteAllBytes(esm, previousOutput);
            var localTimestamp = new DateTimeOffset(2026, 8, 8, 14, 35, 27, TimeSpan.FromHours(-4));

            TesPluginConversionResult result = new TesPluginConverter(new FixedTimeProvider(localTimestamp)).Convert(esp);

            string expectedBackup = $"{esm}.bak.20260808143527";
            Assert.Equal(esm, result.OutputPath);
            Assert.Equal(expectedBackup, result.BackupPath);
            Assert.Equal(previousOutput, File.ReadAllBytes(expectedBackup));
            Assert.True(File.Exists(esm));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Convert_In_Place_Backs_Up_Original_Esm()
    {
        string root = CreateTempRoot();
        try
        {
            string esm = Path.Combine(root, "example.esm");
            byte[] original = BuildTes4Plugin(recordCount: 12, flags: 0x101);
            File.WriteAllBytes(esm, original);
            var localTimestamp = new DateTimeOffset(2026, 8, 8, 14, 35, 27, TimeSpan.FromHours(2));

            TesPluginConversionResult result = new TesPluginConverter(new FixedTimeProvider(localTimestamp))
                .Convert(esm, TesMasterSize.Full);

            Assert.Equal($"{esm}.bak.20260808143527", result.BackupPath);
            Assert.Equal(original, File.ReadAllBytes(result.BackupPath!));
            Assert.Equal(1u, ReadFlags(esm) & 0x501u);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_Extracts_Mat_Path_From_Lmsw_Refl_Blob()
    {
        string root = CreateTempRoot();
        try
        {
            string pluginPath = Path.Combine(root, "sample.esp");
            File.WriteAllBytes(pluginPath, BuildLmswPlugin("Clothes\\DarkStar\\panel.mat"));

            var tes = new TESFile();
            var result = tes.Read(pluginPath);

            Assert.Contains("Data\\Materials\\Clothes\\DarkStar\\panel.mat", result.ReferencedMats, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] BuildLmswPlugin(string matToken)
    {
        byte[] reflBlob = BuildBlobWithEmbeddedNulls(matToken);

        using var payload = new MemoryStream();
        payload.Write(Encoding.ASCII.GetBytes("REFL"));
        payload.WriteByte((byte)(reflBlob.Length & 0xFF));
        payload.WriteByte((byte)((reflBlob.Length >> 8) & 0xFF));
        payload.Write(reflBlob);

        byte[] payloadBytes = payload.ToArray();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("LMSW"));
        bw.Write(payloadBytes.Length); // record data size
        bw.Write(0); // flags
        bw.Write(0); // form id
        bw.Write(0); // revision
        bw.Write((ushort)0); // version
        bw.Write((ushort)0); // unknown
        bw.Write(payloadBytes);
        bw.Flush();

        return ms.ToArray();
    }

    private static byte[] BuildTes4Plugin(uint recordCount, uint flags = 0, bool includeIncc = false)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        bw.Write(Encoding.ASCII.GetBytes("TES4"));
        bw.Write(includeIncc ? 28 : 18); // HEDR plus optional 10-byte INCC subrecord
        bw.Write(flags);
        bw.Write(0u); // form ID
        bw.Write(0u); // revision
        bw.Write((ushort)0);
        bw.Write((ushort)0);
        bw.Write(Encoding.ASCII.GetBytes("HEDR"));
        bw.Write((ushort)12);
        bw.Write(1.0f);
        bw.Write(recordCount);
        bw.Write(0x800u);
        if (includeIncc)
        {
            bw.Write(Encoding.ASCII.GetBytes("INCC"));
            bw.Write((ushort)4);
            bw.Write(0u);
        }
        return ms.ToArray();
    }

    private static uint ReadFlags(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return BitConverter.ToUInt32(bytes, 8);
    }

    private sealed class FixedTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("Test", localNow.Offset, "Test", "Test");

        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();
    }

    private static byte[] BuildBlobWithEmbeddedNulls(string token)
    {
        using var ms = new MemoryStream();
        byte[] prefix = Encoding.ASCII.GetBytes("junk-");
        ms.Write(prefix);

        foreach (byte b in Encoding.ASCII.GetBytes(token))
        {
            ms.WriteByte(b);
            if (b == (byte)'\\')
            {
                ms.WriteByte(0);
            }
        }

        ms.Write(Encoding.ASCII.GetBytes("-tail"));
        return ms.ToArray();
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "dmm-tes-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
