using DirectorySorter.Core;

namespace DirectorySorter.Plugins.ByDate;

/// <summary>Groups files into Year/Month folders based on last-write time, e.g. 2026/08-August.</summary>
public sealed class ByDatePlugin : ISortPlugin
{
    public string Key => "date";
    public string DisplayName => "By Date Modified";

    public string? GetDestinationFolder(FileInfo file, SortContext context)
    {
        var date = file.LastWriteTime;
        return Path.Combine(date.Year.ToString(), $"{date.Month:D2}-{date:MMMM}");
    }
}
