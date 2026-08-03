using DirectorySorter.Core;

namespace DirectorySorter.Watcher;

/// <summary>
/// Runs continuously in the background (e.g. as a scheduled task or Windows service
/// wrapper such as NSSM), watches configured folders for new files, and re-sorts
/// them automatically a few seconds after activity settles down (debounced so a
/// large copy operation doesn't trigger dozens of overlapping sort passes).
/// </summary>
internal static class Program
{
    private static readonly Dictionary<string, System.Timers.Timer> DebounceTimers = new();
    private static readonly object TimerLock = new();

    private static async Task<int> Main()
    {
        var baseDir = AppContext.BaseDirectory;
        var log = new Logger(Path.Combine(baseDir, "Logs", "watcher.log"));
        var configPath = Path.Combine(baseDir, "sorter.config.json");
        var config = SorterConfig.Load(configPath);

        var loader = new PluginLoader(log);
        var plugins = loader.LoadFrom(Path.Combine(baseDir, config.PluginsFolder));

        if (config.WatchFolders.Count == 0)
        {
            log.Warn("No WatchFolders configured in sorter.config.json. Add at least one and restart.");
            return 1;
        }

        var watchers = new List<FileSystemWatcher>();

        foreach (var wf in config.WatchFolders)
        {
            if (!Directory.Exists(wf.Path))
            {
                log.Warn($"Watch folder does not exist, skipping: {wf.Path}");
                continue;
            }

            var plugin = plugins.FirstOrDefault(p => p.Key.Equals(wf.Strategy, StringComparison.OrdinalIgnoreCase));
            if (plugin is null)
            {
                log.Warn($"Strategy '{wf.Strategy}' not found for watch folder {wf.Path}, skipping.");
                continue;
            }

            var fsw = new FileSystemWatcher(wf.Path)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            fsw.Created += (_, e) => ScheduleSort(wf, plugin, log, config, baseDir);
            fsw.Renamed += (_, e) => ScheduleSort(wf, plugin, log, config, baseDir);

            watchers.Add(fsw);
            log.Info($"Watching '{wf.Path}' with strategy '{plugin.DisplayName}' (debounce {wf.DebounceMs}ms)");
        }

        if (watchers.Count == 0)
        {
            log.Error("No valid watch folders were set up. Exiting.");
            return 1;
        }

        log.Info("Watcher running. Press Ctrl+C to stop.");
        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.TrySetResult(); };
        await tcs.Task;

        foreach (var w in watchers)
            w.Dispose();
        loader.UnloadAll();
        return 0;
    }

    private static void ScheduleSort(WatchFolder wf, ISortPlugin plugin, Logger log, SorterConfig config, string baseDir)
    {
        lock (TimerLock)
        {
            if (DebounceTimers.TryGetValue(wf.Path, out var existing))
            {
                existing.Stop();
                existing.Start(); // restart the debounce window
                return;
            }

            var timer = new System.Timers.Timer(wf.DebounceMs) { AutoReset = false };
            timer.Elapsed += (_, _) =>
            {
                try
                {
                    var engine = new SortEngine(log);
                    var options = new SortOptions
                    {
                        DryRun = false,
                        Recursive = false,
                        DetectDuplicates = config.DetectDuplicates,
                        ConflictResolution = config.ConflictResolution
                    };
                    var result = engine.Run(wf.Path, plugin, options, Path.Combine(baseDir, config.JournalFolder));
                    log.Info($"[auto] {wf.Path}: moved {result.FilesMoved}, skipped {result.FilesSkipped}");
                }
                catch (Exception ex)
                {
                    log.Error($"[auto] sort failed for {wf.Path}: {ex.Message}");
                }
                finally
                {
                    lock (TimerLock) { DebounceTimers.Remove(wf.Path); }
                }
            };
            DebounceTimers[wf.Path] = timer;
            timer.Start();
        }
    }
}
