namespace DirectorySorter.Core;

/// <summary>
/// A meta-plugin, itself an <see cref="ISortPlugin"/>, that evaluates user-defined
/// rules from sorter.config.json in order. For the first matching rule it either
/// returns a fixed destination folder or delegates to another loaded plugin by key.
/// Falls back to a configured plugin if nothing matches. This lets you mix hard
/// rules with the built-in strategies without writing a new DLL for one-off cases.
/// </summary>
public sealed class RuleBasedPlugin : ISortPlugin
{
    private readonly List<RuleDefinition> _rules;
    private readonly IReadOnlyDictionary<string, ISortPlugin> _pluginsByKey;
    private readonly ISortPlugin? _fallback;

    public string Key => "rules";
    public string DisplayName => "Custom Rules";

    public RuleBasedPlugin(IEnumerable<RuleDefinition> rules, IEnumerable<ISortPlugin> availablePlugins, string fallbackKey)
    {
        _rules = rules.ToList();
        _pluginsByKey = availablePlugins.ToDictionary(p => p.Key, p => p, StringComparer.OrdinalIgnoreCase);
        _pluginsByKey.TryGetValue(fallbackKey, out _fallback);
    }

    public string? GetDestinationFolder(FileInfo file, SortContext context)
    {
        foreach (var rule in _rules)
        {
            if (!Matches(rule.Match, file))
                continue;

            if (!string.IsNullOrWhiteSpace(rule.UsePlugin) &&
                _pluginsByKey.TryGetValue(rule.UsePlugin, out var plugin))
            {
                return plugin.GetDestinationFolder(file, context);
            }

            if (!string.IsNullOrWhiteSpace(rule.Folder))
                return rule.Folder;
        }

        // No rule matched (or matched rules had no action) -- use the configured fallback strategy.
        return _fallback?.GetDestinationFolder(file, context);
    }

    private static bool Matches(RuleMatch match, FileInfo file)
    {
        if (match.Extensions is { Length: > 0 } exts &&
            !exts.Any(e => e.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!string.IsNullOrWhiteSpace(match.NamePattern) &&
            !System.Text.RegularExpressions.Regex.IsMatch(file.Name, match.NamePattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return false;

        var sizeMB = file.Length / (1024.0 * 1024.0);
        if (match.MinSizeMB.HasValue && sizeMB < match.MinSizeMB.Value) return false;
        if (match.MaxSizeMB.HasValue && sizeMB > match.MaxSizeMB.Value) return false;

        var ageDays = (DateTime.Now - file.LastWriteTime).TotalDays;
        if (match.OlderThanDays.HasValue && ageDays < match.OlderThanDays.Value) return false;
        if (match.NewerThanDays.HasValue && ageDays > match.NewerThanDays.Value) return false;

        return true;
    }
}
