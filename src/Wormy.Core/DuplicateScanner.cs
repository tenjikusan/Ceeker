using System.Security.Cryptography;

namespace Wormy.Core;

public sealed record DuplicateGroup(IReadOnlyList<FileEntry> Files);
public sealed record DuplicateProgress(int Completed, int Candidates, string? FileName = null, long FileBytes = 0, long FileLength = 0, DuplicateGroup? FoundGroup = null);
public sealed record DuplicateResult(IReadOnlyList<DuplicateGroup> Groups, IReadOnlyList<string> Skipped);

public static class DuplicateScanner
{
    public static Task<DuplicateResult> ScanAsync(IEnumerable<FileEntry> entries, IProgress<DuplicateProgress>? progress, CancellationToken token)
        => Task.Run(async () =>
        {
            token.ThrowIfCancellationRequested();
            var candidates = entries.DistinctBy(file => Path.GetFullPath(file.Path), StringComparer.OrdinalIgnoreCase)
                .GroupBy(file => file.Length).Where(group => group.Count() > 1).SelectMany(group => group).ToArray();
            var hashes = new Dictionary<(long, string), List<FileEntry>>();
            var skipped = new List<string>();
            var buffer = new byte[128 * 1024];
            var completed = 0;
            progress?.Report(new(completed, candidates.Length));
            foreach (var file in candidates)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    Validate(file);
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    await using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    int read;
                    var lastProgress = Environment.TickCount64;
                                        while ((read = await stream.ReadAsync(buffer, token)) != 0)
                    {
                        hash.AppendData(buffer, 0, read);
                        if (Environment.TickCount64 - lastProgress >= 250)
                        {
                            progress?.Report(new(completed, candidates.Length, file.Name, stream.Position, file.Length));
                            lastProgress = Environment.TickCount64;
                        }
                    }
                    Validate(file);
                    var key = (file.Length, Convert.ToHexString(hash.GetHashAndReset()));
                    if (!hashes.TryGetValue(key, out var matches)) hashes[key] = matches = [];
                    matches.Add(file with { CreatedUtc = File.GetCreationTimeUtc(file.Path) });
                    if (matches.Count == 2) progress?.Report(new(completed + 1, candidates.Length, FoundGroup: new DuplicateGroup(matches.ToArray())));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { skipped.Add($"{file.Path}: {ex.Message}"); }
                progress?.Report(new(++completed, candidates.Length));
            }
            token.ThrowIfCancellationRequested();
            return new DuplicateResult(hashes.Values.Where(group => group.Count > 1)
                .Select(group => new DuplicateGroup(group.OrderBy(LibraryInsights.IsDownload).ThenBy(file=>file.CreatedUtc ?? DateTime.MaxValue).ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray())).ToArray(), skipped);
        }, token);

    private static void Validate(FileEntry entry)
    {
        var info = new FileInfo(entry.Path);
        if (!info.Exists) throw new IOException("File no longer exists; refresh the library.");
        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0)
            throw new IOException("Linked or offline file skipped.");
        if (info.Length != entry.Length || info.LastWriteTimeUtc != entry.ModifiedUtc)
            throw new IOException("File changed; refresh the library.");
    }
}
