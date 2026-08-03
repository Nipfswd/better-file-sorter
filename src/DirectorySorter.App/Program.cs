using DirectorySorter.Core;

namespace DirectorySorter.App;

internal static class Program
{
    private static int Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var log = new Logger(Path.Combine(baseDir, "Logs", "sorter.log"));

        PrintBanner();

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        var configPath = Path.Combine(baseDir, "sorter.config.json");
        var config = SorterConfig.Load(configPath);

        // --- undo mode -------------------------------------------------
        var undoIndex = Array.IndexOf(args, "--undo");
        if (undoIndex >= 0)
        {
            if (undoIndex + 1 >= args.Length)
            {
                log.Error("--undo requires a path to a journal file.");
                return 1;
            }
            try
            {
                var restored = JournalManager.Undo(args[undoIndex + 1]);
                log.Info($"Undo complete. Restored {restored} file(s).");
                return 0;
            }
            catch (Exception ex)
            {
                log.Error($"Undo failed: {ex.Message}");
                return 1;
            }
        }

        // --- plugin discovery -------------------------------------------
        var pluginsFolder = Path.Combine(baseDir, config.PluginsFolder);
        var loader = new PluginLoader(log);
        var plugins = loader.LoadFrom(pluginsFolder);

        if (args.Contains("--list-plugins"))
        {
            Console.WriteLine("Available plugins:");
            foreach (var p in plugins)
                Console.WriteLine($"  {p.Key,-12} {p.DisplayName}");
            return 0;
        }

        if (plugins.Count == 0)
        {
            log.Error($"No plugins found in '{pluginsFolder}'. Build the Plugins.* projects first.");
            return 1;
        }

        // --- argument parsing --------------------------------------------
        string? rootPath = args.FirstOrDefault(a => !a.StartsWith("--"));
        if (rootPath is null || !Directory.Exists(rootPath))
        {
            log.Error("Usage: DirectorySorter.exe <folder> [--strategy=extension] [--dry-run] [--recursive]");
            return 1;
        }

        string strategyKey = GetOption(args, "--strategy") ?? config.DefaultStrategy;
        var plugin = plugins.FirstOrDefault(p => p.Key.Equals(strategyKey, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
        {
            log.Error($"Unknown strategy '{strategyKey}'. Use --list-plugins to see options.");
            return 1;
        }

        var options = new SortOptions
        {
            DryRun = args.Contains("--dry-run"),
            Recursive = args.Contains("--recursive"),
            DetectDuplicates = config.DetectDuplicates && !args.Contains("--no-dupes"),
            ConflictResolution = GetOption(args, "--on-conflict") ?? config.ConflictResolution,
        };

        log.Info($"Sorting '{rootPath}' with strategy '{plugin.DisplayName}' (dry-run: {options.DryRun}, recursive: {options.Recursive})");

        var engine = new SortEngine(log);
        var result = engine.Run(rootPath, plugin, options, Path.Combine(baseDir, config.JournalFolder));

        log.Info($"Done. Moved: {result.FilesMoved}, Skipped: {result.FilesSkipped}, Duplicates found: {result.DuplicatesFound}");
        if (result.JournalPath is not null)
            log.Info($"Journal written to: {result.JournalPath} (use --undo \"{result.JournalPath}\" to reverse this run)");

        loader.UnloadAll();
        return 0;
    }

    private static string? GetOption(string[] args, string name)
    {
        var prefix = name + "=";
        return args.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================");
        Console.WriteLine("   DirectorySorter  --  plugin edition   ");
        Console.WriteLine("=========================================");
        Console.ResetColor();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Usage:
          DirectorySorter.exe <folder> [options]
          DirectorySorter.exe --undo <journal-file>
          DirectorySorter.exe --list-plugins

        Options:
          --strategy=<key>     Sorting strategy plugin to use (default from sorter.config.json)
          --dry-run            Show what would happen without moving anything
          --recursive          Include subfolders
          --no-dupes           Skip duplicate-content detection
          --on-conflict=<mode> rename | skip | overwrite  (default: rename)

        Examples:
          DirectorySorter.exe C:\Users\me\Downloads --strategy=extension --dry-run
          DirectorySorter.exe C:\Users\me\Downloads --strategy=date --recursive
          DirectorySorter.exe --undo "Journals\sort-journal-20260803-120000.json"
        """);
    }
}
