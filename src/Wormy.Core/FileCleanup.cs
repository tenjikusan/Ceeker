using System.Security.Cryptography;

namespace Wormy.Core;

public sealed record RecycleRequest(FileEntry File, string Sha256);
public static class FileCleanup
{
    public static async Task<RecycleRequest> PrepareAsync(FileEntry file, CancellationToken token) => new(file, await HashAsync(file, token));
    public static async Task RecycleAsync(RecycleRequest request, Action<string> recycle, CancellationToken token)
    {
        var hash = await HashAsync(request.File, token);
        if (!string.Equals(hash, request.Sha256, StringComparison.Ordinal)) throw new IOException("Contents changed after review. Select the file again.");
        token.ThrowIfCancellationRequested(); ThumbnailCache.Validate(request.File);
        recycle(request.File.Path);
    }
    private static async Task<string> HashAsync(FileEntry file, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); ThumbnailCache.Validate(file);
        await using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, token);
        ThumbnailCache.Validate(file); return Convert.ToHexString(hash);
    }
}
