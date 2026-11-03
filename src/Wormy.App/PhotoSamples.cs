using Windows.Graphics.Imaging;
using Windows.Storage;
using Wormy.Core;

namespace Wormy.App;

public static class PhotoSamples
{
    public static Task<PhotoFingerprint> ReadAsync(FileEntry file,CancellationToken token)=>ReadAsync(file,token,false);
    public static async Task<PhotoFingerprint> ReadAsync(FileEntry file, CancellationToken token,bool geometric)
    {
        if (file.Length > 40_000_000) throw new IOException("Image exceeds 40 MB.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15)); var ct = timeout.Token;
        var storage = await StorageFile.GetFileFromPathAsync(file.Path).AsTask(ct);
        using var stream = await storage.OpenReadAsync().AsTask(ct);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct);
        if (decoder.PixelWidth > 16384 || decoder.PixelHeight > 16384 || (long)decoder.PixelWidth * decoder.PixelHeight > 100_000_000) throw new IOException("Image exceeds the pixel limit.");
        var transform = new BitmapTransform { ScaledWidth = 9, ScaledHeight = 9, InterpolationMode = BitmapInterpolationMode.Fant };
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask(ct);
        var square = pixels.DetachPixelData();
        if (square.Length != 9 * 9 * 4) throw new IOException("Unexpected image sample dimensions.");
        var sample = new byte[9 * 8 * 4];
        for (var y = 0; y < 8; y++) for (var x = 0; x < 9; x++) for (var c = 0; c < 4; c++)
        {
            var row = y * 8d / 7; var low = (int)row; var high = Math.Min(8, low + 1); var weight = row - low;
            sample[(y * 9 + x) * 4 + c] = (byte)Math.Round(square[(low * 9 + x) * 4 + c] * (1 - weight) + square[(high * 9 + x) * 4 + c] * weight);
        }
        transform.ScaledWidth = 32; transform.ScaledHeight = 32;
        var detail = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask(ct)).DetachPixelData();
        var aspect=decoder.OrientedPixelWidth/(double)decoder.OrientedPixelHeight;
        PhotoFeatures? features=null;
        if(geometric)
        {
            var ratio=Math.Min(1d,512d/Math.Max(decoder.PixelWidth,decoder.PixelHeight));
            var featureTransform=new BitmapTransform { ScaledWidth=(uint)Math.Max(1,decoder.PixelWidth*ratio),ScaledHeight=(uint)Math.Max(1,decoder.PixelHeight*ratio),InterpolationMode=BitmapInterpolationMode.Fant };
            var gray=(await decoder.GetPixelDataAsync(BitmapPixelFormat.Gray8,BitmapAlphaMode.Ignore,featureTransform,ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(ct)).DetachPixelData();
            var width=(int)Math.Max(1,decoder.OrientedPixelWidth*ratio); var height=(int)Math.Max(1,decoder.OrientedPixelHeight*ratio);
            features=await Task.Run(()=>GeometricPhotos.Extract(gray,width,height),ct);
        }
        return await Task.Run(()=>SimilarPhotos.Fingerprint(sample,aspect) with { PerceptualHash=SimilarPhotos.PerceptualHash(detail),CenterHash=SimilarPhotos.PerceptualHash(detail,true),Variants=SimilarPhotos.Variants(detail,aspect),Features=features },ct);
    }
}
