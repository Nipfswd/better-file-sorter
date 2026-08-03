using DirectorySorter.Core;

namespace DirectorySorter.Plugins.ByExtension;

/// <summary>Groups files into folders named after their extension: Documents/.pdf, Images/.png, etc.</summary>
public sealed class ByExtensionPlugin : ISortPlugin
{
    public string Key => "extension";
    public string DisplayName => "By Extension";

    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "Documents", [".doc"] = "Documents", [".docx"] = "Documents", [".txt"] = "Documents", [".md"] = "Documents",
        [".png"] = "Images", [".jpg"] = "Images", [".jpeg"] = "Images", [".gif"] = "Images", [".webp"] = "Images", [".svg"] = "Images",
        [".mp4"] = "Video", [".mkv"] = "Video", [".mov"] = "Video", [".avi"] = "Video",
        [".mp3"] = "Audio", [".wav"] = "Audio", [".flac"] = "Audio",
        [".zip"] = "Archives", [".rar"] = "Archives", [".7z"] = "Archives",
        [".exe"] = "Executables", [".msi"] = "Executables", [".dll"] = "Executables",
        [".cs"] = "Code", [".py"] = "Code", [".js"] = "Code", [".ts"] = "Code", [".cpp"] = "Code", [".h"] = "Code",
    };

    public string? GetDestinationFolder(FileInfo file, SortContext context)
    {
        var ext = file.Extension;
        if (string.IsNullOrEmpty(ext))
            return "Other/NoExtension";

        return Categories.TryGetValue(ext, out var category)
            ? Path.Combine(category, ext.TrimStart('.').ToUpperInvariant())
            : Path.Combine("Other", ext.TrimStart('.').ToUpperInvariant());
    }
}
