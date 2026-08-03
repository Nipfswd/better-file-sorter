namespace DirectorySorter.Core;

/// <summary>
/// Contract every sorting-strategy plugin DLL must implement.
/// The App and Watcher EXEs discover implementations of this interface
/// at runtime by scanning the Plugins folder for DLLs.
/// </summary>
public interface ISortPlugin
{
    /// <summary>Unique key used on the command line, e.g. "extension", "date", "size".</summary>
    string Key { get; }

    /// <summary>Human-readable name shown in --list-plugins.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Given a file, return the relative sub-folder (under the sort root)
    /// it should be moved into. Return null/empty to leave the file untouched.
    /// </summary>
    string? GetDestinationFolder(FileInfo file, SortContext context);
}
