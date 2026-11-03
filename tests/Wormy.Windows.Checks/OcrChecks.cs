using System.Drawing;
using System.Drawing.Imaging;
using Wormy.App;

internal static class OcrChecks
{
    public static async Task RunAsync(string root)
    {
        var path = Path.Combine(root, "ocr.png");
        using (var bitmap = new Bitmap(1200, 400))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 48, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            graphics.Clear(Color.White);
            graphics.DrawString("WORMY LOCAL OCR", font, Brushes.Black, 50, 60);
            graphics.DrawString("Invoice 12345", font, Brushes.Black, 50, 170);
            bitmap.Save(path, ImageFormat.Png);
        }
        var result = await ImageOcr.ReadAsync(path, CancellationToken.None);
        if (!result.Text.Contains("WORMY") || !result.Text.Contains("12345") || result.Words.Count < 4 || result.Width != 1200 || result.Height != 400)
            throw new Exception($"Generated OCR text/boxes failed: {result.Text}");
        if (result.Words.Any(word => word.X < 0 || word.Y < 0 || word.Width <= 0 || word.Height <= 0 || word.X + word.Width > result.Width || word.Y + word.Height > result.Height))
            throw new Exception("OCR word bounds invalid.");
        if (result.Words.Any(word => string.IsNullOrWhiteSpace(word.Text)) || !result.Words.Any(word => word.Text == "WORMY") || !result.Words.Any(word => word.Text == "12345")) throw new Exception("Word selection text missing.");
        Directory.CreateDirectory("artifacts"); File.Copy(path, "artifacts/ocr-fixture.png", true);
        var originalBytes = await File.ReadAllBytesAsync(path);
        var heicPath = Path.Combine(root,"receipt.heic");
        var encodedHeic = false;
        try
        {
            var storage = await Windows.Storage.StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
            using var input = await storage.OpenReadAsync();
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(input);
            using var pixels = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore);
            var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(root));
            var target = await folder.CreateFileAsync("receipt.heic", Windows.Storage.CreationCollisionOption.FailIfExists);
            using var output = await target.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.HeifEncoderId,output);
            encoder.SetSoftwareBitmap(pixels); await encoder.FlushAsync(); encodedHeic = true;
        }
        catch (Exception ex) { Console.WriteLine("HEIC encoder unavailable: " + ex.Message); }
        if (encodedHeic)
        {
            var before = await File.ReadAllBytesAsync(heicPath);
            var heic = await ImageOcr.ReadAsync(heicPath,CancellationToken.None);
            var after = await File.ReadAllBytesAsync(heicPath);
            if (!heic.Text.Contains("WORMY") || !heic.Text.Contains("12345") || !before.SequenceEqual(after)) throw new Exception("Genuine HEIC recognition or preservation failed.");
            Console.WriteLine("PASS: genuine HEIC text recognition and unchanged source.");
        }
        var originalAfter = await File.ReadAllBytesAsync(path);
        if (!originalBytes.SequenceEqual(originalAfter)) throw new Exception("OCR modified source PNG.");
        if (!ImageOcr.Supports(".HEIC") || !ImageOcr.Supports(".heif") || ImageOcr.Supports(".svg")) throw new Exception("OCR format eligibility failed.");
        using (var large = new Bitmap(4000,3000))
        using (var graphics = Graphics.FromImage(large))
        using (var font = new Font("Arial",160,FontStyle.Regular,GraphicsUnit.Pixel))
        { graphics.Clear(Color.White); graphics.DrawString("WORMY CAMERA 12345",font,Brushes.Black,150,300); large.Save(path,ImageFormat.Png); }
        var largeBytes = await File.ReadAllBytesAsync(path);
        var scaled = await ImageOcr.ReadAsync(path,CancellationToken.None);
        var largeAfter = await File.ReadAllBytesAsync(path);
        if (!scaled.Text.Contains("WORMY") || !scaled.Text.Contains("12345") || scaled.Width>=4000 || scaled.Height>=3000 || (long)scaled.Width*scaled.Height>8_000_000 || scaled.Words.Any(w=>w.X<0 || w.Y<0 || w.X+w.Width>scaled.Width || w.Y+w.Height>scaled.Height) || !largeBytes.SequenceEqual(largeAfter)) throw new Exception("Large-image OCR scaling, recognition, word coordinates or source preservation failed.");
        Console.WriteLine($"PASS: 12MP source recognized at {scaled.Width}x{scaled.Height}; word bounds and original preserved.");
        using (var bitmap = new Bitmap(400, 200))
        using (var graphics = Graphics.FromImage(bitmap))
        { graphics.Clear(Color.White); bitmap.Save(path, ImageFormat.Png); }
        if ((await ImageOcr.ReadAsync(path, CancellationToken.None)).Text.Length != 0) throw new Exception("Blank image produced text.");
        using (var bitmap = new Bitmap(17000, 1)) bitmap.Save(path, ImageFormat.Png);
        try { await ImageOcr.ReadAsync(path, CancellationToken.None); throw new Exception("Oversized OCR image accepted."); }
        catch (IOException) { }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await ImageOcr.ReadAsync(path, cancelled.Token); throw new Exception("Cancelled OCR accepted."); }
        catch (OperationCanceledException) { }
        await File.WriteAllTextAsync(path, "Invalid image");
        var rejected = false;
        try { await ImageOcr.ReadAsync(path, CancellationToken.None); } catch { rejected = true; }
        if (!rejected) throw new Exception("Corrupt OCR image accepted.");
        Console.WriteLine("PASS: offline OCR recognized generated English text/word bounds, blank image, size limits, cancellation and corrupt-image rejection.");
    }
}
