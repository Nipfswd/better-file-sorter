namespace DirectorySorter.Core;

public sealed record SortResult(int FilesMoved, int FilesSkipped, int DuplicatesFound, string? JournalPath);

/// <summary>One file's planned move, for review before anything actually happens (used by the GUI's Preview and CLI --dry-run).</summary>
public sealed record PlannedMove(string SourcePath, string RelativeDestination, bool IsDuplicate);

/// <summary>
/// Orchestrates a single sort pass: enumerate files, ask the active plugin where
/// each one belongs, resolve name conflicts, move files (or simulate in dry-run),
/// and record every move to a journal for undo.
/// </summary>
public sealed class SortEngine
{
    private readonly Logger _log;
    private readonly JournalManager _journal = new();

    public SortEngine(Logger log) => _log = log;

    public SortResult Run(string rootPath, ISortPlugin plugin, SortOptions options, string journalFolder)
    {
        var context = new SortContext { RootPath = rootPath, Options = options };
        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var allFiles = Directory.EnumerateFiles(rootPath, "*", searchOption)
            .Select(p => new FileInfo(p))
            .Where(f => !IsExcluded(f, options.ExcludeGlobs))
            .ToList();

        int duplicatesFound = 0;
        var skipSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (options.DetectDuplicates)
        {
            var dupes = HashUtil.FindDuplicates(allFiles);
            foreach (var group in dupes.Values)
            {
                duplicatesFound += group.Count - 1;
                // Keep the first file in each duplicate group, skip the rest from sorting
                // (they get reported, not silently deleted -- deletion is destructive and opt-in only).
                foreach (var dup in group.Skip(1))
                    skipSet.Add(dup.FullName);
                _log.Warn($"Duplicate group ({group.Count} files, keeping '{group[0].Name}'): " +
                          string.Join(", ", group.Skip(1).Select(f => f.Name)));
            }
        }

        int moved = 0, skipped = 0;
        var lockObj = new object();

        Parallel.ForEach(allFiles,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism) },
            file =>
            {
                if (skipSet.Contains(file.FullName))
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                string? destFolder;
                try
                {
                    destFolder = plugin.GetDestinationFolder(file, context);
                }
                catch (Exception ex)
                {
                    _log.Error($"Plugin threw an exception for '{file.Name}': {ex.Message}");
                    Interlocked.Increment(ref skipped);
                    return;
                }

                if (string.IsNullOrWhiteSpace(destFolder))
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                var targetDir = Path.Combine(rootPath, destFolder);
                var targetPath = Path.Combine(targetDir, file.Name);

                lock (lockObj)
                {
                    targetPath = ResolveConflict(targetPath, options.ConflictResolution);
                    if (targetPath is null)
                    {
                        skipped++;
                        return;
                    }

                    if (!options.DryRun)
                    {
                        Directory.CreateDirectory(targetDir);
                        File.Move(file.FullName, targetPath, overwrite: options.ConflictResolution == "overwrite");
                        _journal.Record(file.FullName, targetPath);
                    }

                    _log.Info($"{(options.DryRun ? "[DRY RUN] Would move" : "Moved")} '{file.Name}' -> '{Path.GetRelativePath(rootPath, targetPath)}'");
                    moved++;
                }
            });

        string? journalPath = null;
        if (!options.DryRun && moved > 0)
            journalPath = _journal.Save(journalFolder);

        return new SortResult(moved, skipped, duplicatesFound, journalPath);
    }

    /// <summary>
    /// Computes what a real run would do without touching the filesystem. Single-threaded
    /// (unlike Run) since it's for on-screen review, not throughput.
    /// </summary>
    public List<PlannedMove> Preview(string rootPath, ISortPlugin plugin, SortOptions options)
    {
        var context = new SortContext { RootPath = rootPath, Options = options };
        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var allFiles = Directory.EnumerateFiles(rootPath, "*", searchOption)
            .Select(p => new FileInfo(p))
            .Where(f => !IsExcluded(f, options.ExcludeGlobs))
            .ToList();

        var dupSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (options.DetectDuplicates)
        {
            var dupes = HashUtil.FindDuplicates(allFiles);
            foreach (var group in dupes.Values)
                foreach (var dup in group.Skip(1))
                    dupSet.Add(dup.FullName);
        }

        var results = new List<PlannedMove>();
        foreach (var file in allFiles)
        {
            string? destFolder;
            try
            {
                destFolder = plugin.GetDestinationFolder(file, context);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(destFolder))
                continue;

            var relativeDest = Path.Combine(destFolder, file.Name);
            results.Add(new PlannedMove(file.FullName, relativeDest, dupSet.Contains(file.FullName)));
        }

        return results;
    }

    private static string? ResolveConflict(string targetPath, string strategy)
    {
        if (!File.Exists(targetPath))
            return targetPath;

        switch (strategy)
        {
            case "overwrite":
                return targetPath;
            case "skip":
                return null;
            case "rename":
            default:
                var dir = Path.GetDirectoryName(targetPath)!;
                var name = Path.GetFileNameWithoutExtension(targetPath);
                var ext = Path.GetExtension(targetPath);
                int i = 1;
                string candidate;
                do
                {
                    candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                    i++;
                } while (File.Exists(candidate));
                return candidate;
        }
    }

    private static bool IsExcluded(FileInfo file, string[] globs)
    {
        foreach (var glob in globs)
        {
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(glob)
                .Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            if (System.Text.RegularExpressions.Regex.IsMatch(file.Name, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }
        return false;
    }
}
