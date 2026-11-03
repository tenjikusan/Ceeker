namespace Wormy.Core;
public static class ProtectedFolders
{
    // Windows volume bookkeeping is not user library content. Only match a direct
    // child of a volume root; a user's nested folder with the same name is retained.
    public static bool IsVolumeMetadata(string path)
    {
        var full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var name=Path.GetFileName(full);
        if(!name.Equals("System Volume Information",StringComparison.OrdinalIgnoreCase) && !name.Equals("$Recycle.Bin",StringComparison.OrdinalIgnoreCase)) return false;
        var parent=Path.GetDirectoryName(full); var root=Path.GetPathRoot(full);
        return parent is not null && root is not null && Path.TrimEndingDirectorySeparator(parent).Equals(Path.TrimEndingDirectorySeparator(root),StringComparison.OrdinalIgnoreCase);
    }
}
