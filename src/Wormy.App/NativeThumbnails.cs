using System.IO.Compression;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Wormy.Core;

namespace Wormy.App;

public static class NativeThumbnails
{
    public static async Task<ThumbnailData?> ReadAsync(FileEntry entry, CancellationToken token)
    {
        if (entry.Length > 1_000_000_000) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var file = await StorageFile.GetFileFromPathAsync(entry.Path).AsTask(ct);
        ThumbnailData? shell = null;
        if (entry.Category == "Images" && entry.Length <= 40_000_000)
        {
            try
            {
                using var source = await file.OpenReadAsync().AsTask(ct);
                var decoder = await BitmapDecoder.CreateAsync(source).AsTask(ct);
                if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 || decoder.PixelWidth > 16384 || decoder.PixelHeight > 16384 || (long)decoder.PixelWidth * decoder.PixelHeight > 100_000_000) return shell;
                var scale = Math.Min(1d,256d / Math.Max(decoder.PixelWidth,decoder.PixelHeight));
                var transform = new BitmapTransform { ScaledWidth = (uint)Math.Max(1,decoder.PixelWidth*scale), ScaledHeight = (uint)Math.Max(1,decoder.PixelHeight*scale) };
                using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,transform,ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(ct);
                using var output = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output).AsTask(ct);
                encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync().AsTask(ct);
                return new(await ReadBytesAsync(output,ct));
            }
            catch (OperationCanceledException) { throw; }
            catch { /* Unsupported codecs keep the shell icon. */ }
        }
        try
        {
            using var shellTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct); shellTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 256, ThumbnailOptions.ResizeThumbnail).AsTask(shellTimeout.Token);
            if (thumbnail is not null && thumbnail.Size > 0) shell = new(await ReadBytesAsync(thumbnail, shellTimeout.Token), thumbnail.Type == ThumbnailType.Icon);
            if (shell is { IsIcon: false }) return shell;
        }
        catch (OperationCanceledException) when(!ct.IsCancellationRequested) { /* A slow shell provider must not prevent a local fallback. */ }
        catch (OperationCanceledException) { throw; }
        catch { /* Other providers may still supply a preview. */ }
        if (entry.Extension == ".pdf" && entry.Length <= 20_000_000)
        {
            try
            {
                var document = await PdfDocument.LoadFromFileAsync(file).AsTask(ct);
                if (document.PageCount > 0)
                {
                    using var page = document.GetPage(0);
                    var scale = 512 / Math.Max(page.Size.Width, page.Size.Height);
                    using var stream = new InMemoryRandomAccessStream();
                    await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(1, page.Size.Width * scale), DestinationHeight = (uint)Math.Max(1, page.Size.Height * scale) }).AsTask(ct);
                    return new(await ReadBytesAsync(stream, ct));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
        if (entry.Extension is ".docx" or ".xlsx" or ".pptx" && entry.Length <= 20_000_000)
        {
            var embedded = await Task.Run(async () =>
            {
                try
                {
                    using var archive = ZipFile.OpenRead(entry.Path);
                    var preview = archive.Entries.FirstOrDefault(e => e.FullName.Equals("docProps/thumbnail.jpeg", StringComparison.OrdinalIgnoreCase) || e.FullName.Equals("docProps/thumbnail.jpg", StringComparison.OrdinalIgnoreCase) || e.FullName.Equals("docProps/thumbnail.png", StringComparison.OrdinalIgnoreCase));
                    if (preview is null || preview.Length <= 0 || preview.Length > 2_000_000) return null;
                    using var source = preview.Open(); using var output = new MemoryStream();
                    var buffer = new byte[16_384]; int read;
                    while ((read = await source.ReadAsync(buffer, ct)) > 0)
                    { if (output.Length + read > 2_000_000) throw new IOException("Embedded thumbnail exceeds the size limit."); await output.WriteAsync(buffer.AsMemory(0, read), ct); }
                    var data = output.ToArray(); await ValidateImageAsync(data, ct); return new ThumbnailData(data);
                }
                catch (OperationCanceledException) { throw; }
                catch { return null; }
            }, ct);
            if (embedded is not null) return embedded;
        }
        return shell;
    }

    private static async Task<byte[]> ReadBytesAsync(IRandomAccessStream stream, CancellationToken token)
    {
        if (stream.Size > 2_000_000) throw new IOException("Thumbnail exceeds the image limit.");
        stream.Seek(0); var data = new byte[(int)stream.Size];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)data.Length).AsTask(token); reader.ReadBytes(data);
        await ValidateImageAsync(data, token); return data;
    }
    private static async Task ValidateImageAsync(byte[] data, CancellationToken token)
    {
        using var stream = new InMemoryRandomAccessStream(); using var writer = new DataWriter(stream);
        writer.WriteBytes(data); await writer.StoreAsync().AsTask(token); stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
        if (decoder.PixelWidth > 1024 || decoder.PixelHeight > 1024 || (long)decoder.PixelWidth * decoder.PixelHeight > 1_000_000) throw new IOException("Thumbnail pixel limit exceeded.");
    }
}

