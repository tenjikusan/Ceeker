using System.Text;

namespace Wormy.Core;

public sealed record TextPreviewResult(string Text, bool Truncated);
public static class TextPreview
{
    public const int MaximumCharacters = 64_000;
    public static async Task<TextPreviewResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[MaximumCharacters + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(count), cancellationToken);
            if (read == 0) break;
            count += read;
        }
        if (buffer.AsSpan(0, count).Contains('\0')) throw new InvalidDataException("This file appears to contain binary data.");
        return new(new string(buffer, 0, Math.Min(count, MaximumCharacters)), count > MaximumCharacters);
    }
}
