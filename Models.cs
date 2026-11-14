
using System.Collections.Concurrent;

namespace ForbiddenWords;

public class FoundFile
{
    public string OriginalPath { get; set; } = "";
    public string CopyPath { get; set; } = "";
    public string RedactedPath { get; set; } = "";
    public long Size { get; set; }
    public int Replacements { get; set; }
}

public class ScanResult
{
    public ConcurrentBag<FoundFile> Files { get; set; } = new();

    public ConcurrentDictionary<string, int> WordCounts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int Checked { get; set; }
}

public class ScanProgress
{
    public int Done { get; set; }
    public int Total { get; set; }
    public int Found { get; set; }
    public string CurrentFile { get; set; } = "";
}
