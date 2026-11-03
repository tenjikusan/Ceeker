using Tesseract;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Wormy.App;

public sealed record OcrWordBox(int X, int Y, int Width, int Height, string Text = "");
public sealed record ImageOcrResult(string Text, int Width, int Height, IReadOnlyList<OcrWordBox> Words);

public static class ImageOcr
{
    public static string WindowsLanguage { get; set; } = "";
    public static string LanguageDescription => WindowsLanguage.Length == 0 ? "English" : WindowsLanguage == "auto" ? "Automatic English/Hindi/Telugu/Tamil" : WindowsLanguage == "profile" ? "Windows language preferences" : WindowsLanguage;
    private static readonly SemaphoreSlim Worker = new(1);
    public static bool Supports(string extension) => extension.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".heic" or ".heif";
    public static async Task<ImageOcrResult> ReadAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Image is no longer available.");
        if (info.Length > 40_000_000) throw new IOException("OCR is limited to images up to 40 MB.");
        if ((info.Attributes & (System.IO.FileAttributes.Offline | System.IO.FileAttributes.ReparsePoint)) != 0) throw new IOException("Linked or offline images are not supported.");
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask(token);
        using var source = await file.OpenReadAsync().AsTask(token);
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await Windows.Storage.Streams.RandomAccessStream.CopyAsync(source, stream).AsTask(token);
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
        var pixels = (long)decoder.PixelWidth * decoder.PixelHeight;
        if (decoder.PixelWidth > 16384 || decoder.PixelHeight > 16384 || pixels > 100_000_000 || pixels == 0)
            throw new IOException("OCR source exceeds 16,384 pixels per side or 100 megapixels. Resize a copy and try again.");
        var scale = Math.Min(1d, Math.Min(4096d / Math.Max(decoder.PixelWidth, decoder.PixelHeight), Math.Sqrt(8_000_000d / pixels)));
        var transform = new BitmapTransform { ScaledWidth = (uint)Math.Max(1, Math.Floor(decoder.PixelWidth * scale)), ScaledHeight = (uint)Math.Max(1, Math.Floor(decoder.PixelHeight * scale)), InterpolationMode = BitmapInterpolationMode.Fant };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask(token);
        using var encoded = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, encoded).AsTask(token);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync().AsTask(token);
        encoded.Seek(0);
        var bytes = new byte[checked((int)encoded.Size)];
        using (var reader = new Windows.Storage.Streams.DataReader(encoded.GetInputStreamAt(0)))
        { await reader.LoadAsync((uint)bytes.Length).AsTask(token); reader.ReadBytes(bytes); }
        return await ReadPngAsync(bytes, token);
    }

    public static async Task<ImageOcrResult> ReadPngAsync(byte[] bytes, CancellationToken token, bool highAccuracy = false)
    {
        await Worker.WaitAsync(token);
        try
        {
            if (WindowsLanguage.Length > 0 && WindowsLanguage != "auto") return await WindowsOcrReader.ReadAsync(bytes,WindowsLanguage,token);
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
                var language = WindowsLanguage == "auto" ? "eng+hin+tel+tam" : highAccuracy ? "eng_best" : "eng";
                if (language.Split('+').Any(code=>!File.Exists(Path.Combine(dataPath,code+".traineddata")))) throw new IOException("OCR language data is missing. Extract the full Ceeker package again.");
                using var engine = new TesseractEngine(dataPath, language, EngineMode.LstmOnly);
                using var pix = Pix.LoadFromMemory(bytes);
                if (pix.Width > 4096 || pix.Height > 4096 || (long)pix.Width * pix.Height > 8_000_000)
                    throw new IOException("OCR render exceeds the pixel limit.");
                using var page = engine.Process(pix);
                var text = page.GetText().Trim();
                var words = new List<OcrWordBox>();
                using var iterator = page.GetIterator();
                iterator.Begin();
                do
                {
                    var wordText = iterator.GetText(PageIteratorLevel.Word)?.Trim();
                    if (!string.IsNullOrWhiteSpace(wordText) && iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var box)) words.Add(new(box.X1, box.Y1, box.Width, box.Height, wordText));
                } while (iterator.Next(PageIteratorLevel.Word));
                token.ThrowIfCancellationRequested();
                return new ImageOcrResult(text, pix.Width, pix.Height, words);
            }, token);
        }
        finally { Worker.Release(); }
    }
}
