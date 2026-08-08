using DMM.AssetManagers.TES;

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0 || args.Any(a => a is "-h" or "--help" or "/?"))
    {
        PrintUsage();
        return args.Length == 0 ? 1 : 0;
    }

    TesMasterSize? requestedSize = null;
    TesPluginOutputType outputType = TesPluginOutputType.Automatic;
    var paths = new List<string>();
    foreach (string argument in args)
    {
        TesMasterSize? parsedSize = argument.ToLowerInvariant() switch
        {
            "--small" => TesMasterSize.Small,
            "--medium" or "--med" => TesMasterSize.Medium,
            "--full" or "--large" => TesMasterSize.Full,
            _ => null
        };

        if (argument.Equals("--esp", StringComparison.OrdinalIgnoreCase))
        {
            if (outputType != TesPluginOutputType.Automatic || requestedSize is not null)
                return Fail("--esp cannot be combined with a master-size option.");
            outputType = TesPluginOutputType.Esp;
        }
        else if (parsedSize is not null)
        {
            if (requestedSize is not null || outputType == TesPluginOutputType.Esp)
                return Fail("Specify only one of --small, --medium, or --full.");
            requestedSize = parsedSize;
            outputType = TesPluginOutputType.Esm;
        }
        else if (argument.StartsWith('-'))
        {
            return Fail($"Unknown option: {argument}");
        }
        else
        {
            paths.Add(argument);
        }
    }

    if (paths.Count == 0)
        return Fail("Provide at least one .esp or .esm file.");

    var converter = new TesPluginConverter();
    bool failed = false;
    foreach (string path in paths)
    {
        try
        {
            TesPluginConversionResult result = converter.Convert(path, requestedSize, outputType);
            string kind = result.MasterSize is null
                ? "ESP"
                : $"{result.MasterSize.Value.ToString().ToLowerInvariant()} ESM";
            Console.WriteLine($"{Path.GetFileName(result.InputPath)} -> {Path.GetFileName(result.OutputPath)} " +
                              $"({result.RecordCount} records, {kind})");
            if (result.BackupPath is not null)
                Console.WriteLine($"  Existing output backed up as {Path.GetFileName(result.BackupPath)}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"espesmswap: {path}: {ex.Message}");
            failed = true;
        }
    }

    return failed ? 1 : 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"espesmswap: {message}");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage: espesmswap [--small|--medium|--full|--esp] <plugin.esp|plugin.esm> [...]");
    Console.WriteLine("  ESP input: creates an ESM sized automatically from its HEDR record count.");
    Console.WriteLine("  ESM input: creates an ESP flagged for the full-master layout.");
    Console.WriteLine("  --small, --medium, --full: produce that ESM size when its record count fits.");
    Console.WriteLine("  --esp: explicitly produce a full-layout ESP from an ESM.");
}
