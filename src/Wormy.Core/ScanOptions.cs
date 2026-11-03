namespace Wormy.Core;

public sealed class ScanOptions
{
    public bool IncludeHiddenFiles { get; init; } = true;
    public int MaximumFiles { get; init; } = 100_000;
    public IReadOnlyList<string> ExcludedFolderNames { get; init; } = [];

    public static string[] NormalizeExclusions(IEnumerable<string>? names) => (names ?? [])
        .Select(n => n?.Trim() ?? "").Where(n => n.Length > 0 && n.Length <= 255 && n is not ("." or "..") && !n.Any(c => c < 32 || "\\/:*?\"<>|".Contains(c)))
        .Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
}
