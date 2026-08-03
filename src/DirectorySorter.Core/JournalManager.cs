using System.Text.Json;

namespace DirectorySorter.Core;

/// <summary>One recorded file move, enough information to reverse it.</summary>
public sealed record JournalEntry(string OriginalPath, string NewPath, DateTime TimestampUtc);

/// <summary>
/// Writes a JSON journal of every move performed during a run so that
/// "DirectorySorter.exe --undo &lt;journal-file&gt;" can put everything back.
/// </summary>
public sealed class JournalManager
{
    private readonly List<JournalEntry> _entries = new();
    private readonly object _lock = new();

    public void Record(string originalPath, string newPath)
    {
        lock (_lock)
        {
            _entries.Add(new JournalEntry(originalPath, newPath, DateTime.UtcNow));
        }
    }

    public string Save(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"sort-journal-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }

    public static int Undo(string journalPath)
    {
        if (!File.Exists(journalPath))
            throw new FileNotFoundException("Journal file not found.", journalPath);

        var json = File.ReadAllText(journalPath);
        var entries = JsonSerializer.Deserialize<List<JournalEntry>>(json) ?? new();

        int restored = 0;
        // Undo in reverse order in case later moves depended on earlier folder creation.
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            try
            {
                if (File.Exists(entry.NewPath) && !File.Exists(entry.OriginalPath))
                {
                    var dir = Path.GetDirectoryName(entry.OriginalPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    File.Move(entry.NewPath, entry.OriginalPath);
                    restored++;
                }
            }
            catch (IOException)
            {
                // Leave this one alone and keep undoing the rest.
            }
        }
        return restored;
    }
}
