using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
namespace Wormy.App;
public static partial class WindowsOcrReader
{
    public static async Task<ImageOcrResult> ReadAsync(byte[] bytes,string language,CancellationToken token)
    {
        var engine = language == "profile" ? OcrEngine.TryCreateFromUserProfileLanguages() : OcrEngine.TryCreateFromLanguage(new Language(language));
        if (engine is null) throw new IOException("That Windows OCR language is unavailable. Choose bundled English or an installed language.");
        using var stream = new InMemoryRandomAccessStream(); using var writer = new DataWriter(stream);
        writer.WriteBytes(bytes); await writer.StoreAsync().AsTask(token); stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
        if (decoder.PixelWidth > 4096 || decoder.PixelHeight > 4096 || (long)decoder.PixelWidth*decoder.PixelHeight > 8_000_000) throw new IOException("OCR render exceeds pixel limits.");
        var scale = Math.Min(1d,(double)OcrEngine.MaxImageDimension/Math.Max(decoder.PixelWidth,decoder.PixelHeight));
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore,new BitmapTransform { ScaledWidth=(uint)Math.Max(1,Math.Floor(decoder.PixelWidth*scale)),ScaledHeight=(uint)Math.Max(1,Math.Floor(decoder.PixelHeight*scale)) },ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(token);
        var result = await engine.RecognizeAsync(bitmap).AsTask(token);
        var ratioX=(double)decoder.PixelWidth/bitmap.PixelWidth; var ratioY=(double)decoder.PixelHeight/bitmap.PixelHeight;
        var words=result.Lines.SelectMany(line=>line.Words).Select(word=>new OcrWordBox((int)(word.BoundingRect.X*ratioX),(int)(word.BoundingRect.Y*ratioY),(int)Math.Ceiling(word.BoundingRect.Width*ratioX),(int)Math.Ceiling(word.BoundingRect.Height*ratioY),word.Text)).ToArray();
        return new(result.Text,(int)decoder.PixelWidth,(int)decoder.PixelHeight,words);
    }
}
