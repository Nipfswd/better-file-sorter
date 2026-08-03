using DirectorySorter.Core;

namespace DirectorySorter.Plugins.BySize;

/// <summary>Groups files into buckets by size: Tiny (&lt;100KB), Small (&lt;10MB), Medium (&lt;100MB), Large (&gt;=100MB).</summary>
public sealed class BySizePlugin : ISortPlugin
{
    public string Key => "size";
    public string DisplayName => "By File Size";

    public string? GetDestinationFolder(FileInfo file, SortContext context)
    {
        return file.Length switch
        {
            < 100 * 1024 => "Tiny (under 100KB)",
            < 10 * 1024 * 1024 => "Small (under 10MB)",
            < 100 * 1024 * 1024 => "Medium (under 100MB)",
            _ => "Large (100MB and up)"
        };
    }
}
