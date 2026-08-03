namespace DirectorySorter.Core;

/// <summary>Shared, read-only context passed to every plugin call.</summary>
public sealed class SortContext
{
    public required string RootPath { get; init; }
    public required SortOptions Options { get; init; }
}

public sealed class SortOptions
{
    public bool DryRun { get; set; }
    public bool Recursive { get; set; }
    public bool DetectDuplicates { get; set; } = true;
    public string ConflictResolution { get; set; } = "rename"; // rename | skip | overwrite
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    public string[] ExcludeGlobs { get; set; } = Array.Empty<string>();
}
