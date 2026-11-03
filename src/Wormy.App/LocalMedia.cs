using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Wormy.Core;

namespace Wormy.App;

public sealed class LocalMedia : IDisposable
{
    public MediaPlayer Player { get; } = new() { AutoPlay = false };
    private MediaSource? source;
    private bool disposed;
    public async Task OpenAsync(FileEntry entry, CancellationToken token)
    {
        if (entry.Category is not ("Music" or "Videos")) throw new NotSupportedException("Choose an audio or video file.");
        await Task.Run(() => ThumbnailCache.Validate(entry), token);
        var file = await StorageFile.GetFileFromPathAsync(entry.Path).AsTask(token);
        token.ThrowIfCancellationRequested();
        if (disposed) throw new ObjectDisposedException(nameof(LocalMedia));
        ThumbnailCache.Validate(entry);
        source = MediaSource.CreateFromStorageFile(file);
        Player.Source = source;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Player.Pause(); Player.Source = null; Player.Dispose(); source?.Dispose(); source = null;
    }
}
