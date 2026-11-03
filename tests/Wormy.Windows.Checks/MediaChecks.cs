using Windows.Media.Playback;
using Wormy.App;
using Wormy.Core;

internal static class MediaChecks
{
    public static async Task RunAsync(string root)
    {
        Directory.CreateDirectory(root);
        var wave = Path.Combine(root, "Generated.wav");
        using (var stream = File.Create(wave))
        using (var writer = new BinaryWriter(stream))
        {
            const int samples = 44100 * 3;
            writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(samples * 2);
            for (var i = 0; i < samples; i++) writer.Write((short)0);
        }
        var video = Path.Combine(root, "Generated.mp4");
        File.Copy(Path.GetFullPath("artifacts/thumbnail-fixture-1/files/Preview.mp4"), video, false);
        foreach (var path in new[] { wave, video })
        {
            var before = await File.ReadAllBytesAsync(path);
            using var media = new LocalMedia(); media.Player.IsMuted = true;
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            media.Player.MediaOpened += (_, _) => ready.TrySetResult();
            media.Player.MediaFailed += (_, args) => ready.TrySetException(new Exception(args.ErrorMessage));
            var info = new FileInfo(path); var entry = new FileEntry(path, info.Name, info.Length, info.LastWriteTimeUtc);
            await media.OpenAsync(entry, CancellationToken.None); await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (media.Player.AutoPlay || media.Player.PlaybackSession.NaturalDuration <= TimeSpan.Zero) throw new Exception("Paused media opening failed.");
            media.Player.Play(); await Task.Delay(250);
            if (media.Player.PlaybackSession.Position <= TimeSpan.Zero) throw new Exception("Playback position did not advance.");
            media.Player.Pause(); media.Player.PlaybackSession.Position = TimeSpan.FromMilliseconds(100);
            await Task.Delay(150);
            if (Math.Abs(media.Player.PlaybackSession.Position.TotalMilliseconds - 100) > 75) throw new Exception("Seek/pause failed.");
            media.Dispose(); media.Dispose();
            var after = await File.ReadAllBytesAsync(path); if (!before.SequenceEqual(after)) throw new Exception("Media original changed.");
            Console.WriteLine($"PASS: {info.Name} paused open, duration, muted playback, pause/seek, idempotent disposal and original preservation.");
        }
        var bad = Path.Combine(root, "Broken.mp4"); await File.WriteAllTextAsync(bad, "Not video");
        using (var media = new LocalMedia())
        {
            var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            media.Player.MediaFailed += (_, _) => failed.TrySetResult();
            var info = new FileInfo(bad); await media.OpenAsync(new(bad, info.Name, info.Length, info.LastWriteTimeUtc), CancellationToken.None);
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        using (var media = new LocalMedia())
        using (var token = new CancellationTokenSource())
        {
            token.Cancel(); var info = new FileInfo(wave);
            try { await media.OpenAsync(new(wave, info.Name, info.Length, info.LastWriteTimeUtc), token.Token); throw new Exception("Media cancellation ignored."); }
            catch (OperationCanceledException) { }
            var old = new FileEntry(wave, info.Name, info.Length, info.LastWriteTimeUtc); await File.AppendAllTextAsync(wave, "changed");
            try { await media.OpenAsync(old, CancellationToken.None); throw new Exception("Stale media accepted."); } catch (IOException) { }
        }
        Console.WriteLine("PASS: corrupt media failure, cancellation and stale-file rejection.");
    }
}
