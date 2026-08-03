using System.Text.Json;

namespace DirectorySorter.Core;

/// <summary>Persisted configuration, loaded from sorter.config.json next to the EXE.</summary>
public sealed class SorterConfig
{
    public string PluginsFolder { get; set; } = "Plugins";
    public string JournalFolder { get; set; } = "Journals";
    public string DefaultStrategy { get; set; } = "extension";
    public bool DetectDuplicates { get; set; } = true;
    public string ConflictResolution { get; set; } = "rename";
    public List<WatchFolder> WatchFolders { get; set; } = new();

    public static SorterConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = new SorterConfig();
            Save(defaultConfig, path);
            return defaultConfig;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SorterConfig>(json) ?? new SorterConfig();
    }

    public static void Save(SorterConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}

public sealed class WatchFolder
{
    public string Path { get; set; } = "";
    public string Strategy { get; set; } = "extension";
    public int DebounceMs { get; set; } = 2000;
}
