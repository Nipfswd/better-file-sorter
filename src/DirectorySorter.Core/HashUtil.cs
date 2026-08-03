using System.Security.Cryptography;

namespace DirectorySorter.Core;

/// <summary>SHA-256 based content hashing used to detect duplicate files during a sort.</summary>
public static class HashUtil
{
    public static string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Scans a set of files and groups any whose content is byte-identical.
    /// Cheap size pre-check avoids hashing files that can't possibly match.
    /// </summary>
    public static Dictionary<string, List<FileInfo>> FindDuplicates(IEnumerable<FileInfo> files)
    {
        var bySize = files.GroupBy(f => f.Length).Where(g => g.Count() > 1);
        var result = new Dictionary<string, List<FileInfo>>();

        foreach (var sizeGroup in bySize)
        {
            var byHash = new Dictionary<string, List<FileInfo>>();
            foreach (var file in sizeGroup)
            {
                try
                {
                    var hash = ComputeHash(file.FullName);
                    if (!byHash.TryGetValue(hash, out var list))
                        byHash[hash] = list = new List<FileInfo>();
                    list.Add(file);
                }
                catch (IOException)
                {
                    // File in use or unreadable; skip it rather than fail the whole scan.
                }
            }

            foreach (var kvp in byHash.Where(kvp => kvp.Value.Count > 1))
                result[kvp.Key] = kvp.Value;
        }

        return result;
    }
}
