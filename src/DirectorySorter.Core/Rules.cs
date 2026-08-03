namespace DirectorySorter.Core;

/// <summary>Conditions a file must satisfy for a rule to apply. All set conditions must match (AND).</summary>
public sealed class RuleMatch
{
    public string[]? Extensions { get; set; }
    public string? NamePattern { get; set; }     // regex, matched against the file name
    public double? MinSizeMB { get; set; }
    public double? MaxSizeMB { get; set; }
    public int? OlderThanDays { get; set; }      // based on LastWriteTime
    public int? NewerThanDays { get; set; }
}

/// <summary>
/// One entry in sorter.config.json's "Rules" list. Rules are evaluated top to bottom;
/// the first one that matches wins. A rule either sends the file to a fixed folder,
/// or hands off to another loaded plugin (so you can say "anything named IMG_* use
/// the date plugin, everything else use extension").
/// </summary>
public sealed class RuleDefinition
{
    public string Name { get; set; } = "";
    public RuleMatch Match { get; set; } = new();
    public string? Folder { get; set; }       // fixed/relative destination folder
    public string? UsePlugin { get; set; }    // delegate to another plugin's Key
}
