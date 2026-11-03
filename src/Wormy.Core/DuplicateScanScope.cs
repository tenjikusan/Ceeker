namespace Wormy.Core;
public static class DuplicateScanScope
{
    // Null selects the whole index; an empty selection selects no files.
    public static FileEntry[] Select(IEnumerable<FileEntry> entries, IEnumerable<string>? folders)
    {
        var paths = folders?.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return entries.Where(file => paths is null || paths.Any(folder => FileBrowsing.InFolder(file,folder)))
            .DistinctBy(file => Path.GetFullPath(file.Path),StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
