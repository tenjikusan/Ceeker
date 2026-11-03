using System.Security.Cryptography;

namespace Wormy.Core;

public sealed record CleanupChoice(FileEntry Keep, FileEntry Remove);

public static class DuplicateCleanup
{
    // Keep the retained copy open without write/delete sharing throughout the operation.
    public static async Task RevalidateAndRemoveAsync(CleanupChoice choice, Action<string> recycle, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.Equals(Path.GetFullPath(choice.Keep.Path), Path.GetFullPath(choice.Remove.Path), StringComparison.OrdinalIgnoreCase))
            throw new IOException("The retained copy cannot be removed.");
        Validate(choice.Keep); Validate(choice.Remove);
        await using var kept = new FileStream(choice.Keep.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var keepHash = await SHA256.HashDataAsync(kept, token);
        await using (var removed = new FileStream(choice.Remove.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var removeHash = await SHA256.HashDataAsync(removed, token);
            if (!keepHash.AsSpan().SequenceEqual(removeHash)) throw new IOException("Contents no longer match. Run a new duplicate scan.");
            Validate(choice.Keep); Validate(choice.Remove);
        }
        token.ThrowIfCancellationRequested();
        Validate(choice.Remove);
        recycle(choice.Remove.Path);
    }

    private static void Validate(FileEntry entry)
    {
        var info = new FileInfo(entry.Path);
        if (!info.Exists || info.Length != entry.Length || info.LastWriteTimeUtc != entry.ModifiedUtc)
            throw new IOException("File changed or disappeared. Refresh the library and scan again.");
        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0)
            throw new IOException("Linked or offline files cannot be cleaned up.");
        for (var parent = info.Directory; parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Folders containing links cannot be cleaned up.");
    }
}
