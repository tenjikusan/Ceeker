using System.Drawing;
using System.Drawing.Imaging;
using Wormy.Core;
using Wormy.App;
using Wormy.Infrastructure;

internal static class PhotoReviewChecks
{
    public static async Task RunAsync(string root, bool recycle = true)
    {
        if (Directory.Exists(root)) throw new IOException("Use a new fixture folder.");
        var folder = Path.Combine(root,"Downloads"); Directory.CreateDirectory(folder);
        using (var bitmap = new Bitmap(360,240))
        {
            for(var y=0;y<240;y++) for(var x=0;x<360;x++) { var value=(int)(70+60*Math.Sin(x/22d)+45*Math.Cos(y/18d)); bitmap.SetPixel(x,y,Color.FromArgb(Math.Clamp(value,0,255),Math.Clamp(value+35,0,255),Math.Clamp(value+65,0,255))); }
            bitmap.Save(Path.Combine(folder,"Screenshot-original.png"),ImageFormat.Png);
            using var smaller = new Bitmap(bitmap,new Size(180,120)); smaller.Save(Path.Combine(folder,"Photo-resized.jpg"),ImageFormat.Jpeg);
            using var edited = new Bitmap(bitmap);
            for(var y=0;y<240;y++) for(var x=0;x<360;x++) { var color=edited.GetPixel(x,y); edited.SetPixel(x,y,Color.FromArgb(Math.Min(255,color.R+15),Math.Min(255,color.G+15),Math.Min(255,color.B+15))); }
            edited.Save(Path.Combine(folder,"Photo-brighter.jpg"),ImageFormat.Jpeg);
            using var shifted = new Bitmap(360,240); using(var shiftedGraphics=Graphics.FromImage(shifted)) { shiftedGraphics.Clear(Color.White); shiftedGraphics.DrawImageUnscaled(bitmap,-2,-1); } shifted.Save(Path.Combine(folder,"Photo-small-motion.jpg"),ImageFormat.Jpeg);
        }
        using (var different = new Bitmap(360,240))
        {
            for(var y=0;y<240;y++) for(var x=0;x<360;x++) different.SetPixel(x,y,((x/70+y/50)%2)==0 ? Color.Black : Color.Yellow);
            different.Save(Path.Combine(folder,"Different.png"),ImageFormat.Png);
        }
        await File.WriteAllTextAsync(Path.Combine(folder,"Broken.png"),"not an image");
        var note = Path.Combine(folder,"Old-note.txt"); await File.WriteAllTextAsync(note,"Generated file for cancel-only UI review."); File.SetLastWriteTimeUtc(note,DateTime.UtcNow.AddDays(-61));
        File.SetLastWriteTimeUtc(Path.Combine(folder,"Screenshot-original.png"),DateTime.UtcNow.AddDays(-61));
        var entries=(await FolderScanner.ScanAsync(folder,CancellationToken.None)).Files;
        var originals = new Dictionary<string,byte[]>(); foreach(var file in entries) originals[file.Path]=await File.ReadAllBytesAsync(file.Path);
        var result = await SimilarPhotos.FindAsync(entries, PhotoSamples.ReadAsync, CancellationToken.None);
        if(result.Compared!=5 || result.Skipped!=1 || result.Pairs.Count!=6 || result.Pairs.Any(p=>p.First.Name=="Different.png" || p.Second.Name=="Different.png")) throw new Exception("Native edited/resized/small-motion photo matching or unrelated/corrupt handling failed: "+System.Text.Json.JsonSerializer.Serialize(result));
        foreach(var original in originals) { var after=await File.ReadAllBytesAsync(original.Key); if(!original.Value.SequenceEqual(after)) throw new Exception("Similarity scan changed original."); }
        var heicPath = Path.Combine(folder,"Photo-native.heic");
        var encoded = false;
        try
        {
            var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(Path.Combine(folder,"Screenshot-original.png"));
            using var input = await source.OpenReadAsync();
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(input);
            using var pixels = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore);
            var destination = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folder);
            var outputFile = await destination.CreateFileAsync("Photo-native.heic", Windows.Storage.CreationCollisionOption.FailIfExists);
            using var output = await outputFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.HeifEncoderId, output);
            encoder.SetSoftwareBitmap(pixels); await encoder.FlushAsync(); encoded = true;
        }
        catch (Exception ex) { Console.WriteLine("HEIC encoder unavailable on this host: " + ex.Message); }
        if (encoded)
        {
            var heicInfo = new FileInfo(heicPath);
            var heic = await PhotoSamples.ReadAsync(new(heicPath,heicInfo.Name,heicInfo.Length,heicInfo.LastWriteTimeUtc),CancellationToken.None);
            var jpeg = await PhotoSamples.ReadAsync(entries.Single(e=>e.Name=="Photo-resized.jpg"),CancellationToken.None);
            if (SimilarPhotos.Distance(heic,jpeg) is null) throw new Exception("Genuine HEIC/JPEG matching failed.");
            Console.WriteLine("PASS: genuine Windows-encoded HEIC matched the resized JPEG.");
        }
        await new LibraryIndex(Path.Combine(root,"library.db")).ReplaceRootAsync(folder,entries,CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root,"library.db.view.json"),"{\"LargeFileMB\":1,\"DownloadAgeDays\":30,\"ScreenshotAgeDays\":30}");
        if (!recycle) { Console.WriteLine("PASS: resized, brighter and small-motion JPEG candidates, unrelated/corrupt exclusions and unchanged originals. Fixture: "+root); return; }
        var recyclePath=Path.Combine(root,"Disposable-recycle.txt"); await File.WriteAllTextAsync(recyclePath,"Generated general-recycle integration file."); var info=new FileInfo(recyclePath);
        var request=await FileCleanup.PrepareAsync(new(recyclePath,info.Name,info.Length,info.LastWriteTimeUtc),CancellationToken.None);
        await FileCleanup.RecycleAsync(request, path =>
        {
            var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker=new Thread(() => { try { RecycleBin.Move(path); completion.SetResult(); } catch(Exception ex){completion.SetException(ex);} });
            worker.SetApartmentState(ApartmentState.STA); worker.Start(); completion.Task.GetAwaiter().GetResult();
        }, CancellationToken.None);
        if(File.Exists(recyclePath)) throw new Exception("Generated general file was not recycled.");
        Console.WriteLine("PASS: native resized/JPEG photo candidates, unrelated/corrupt exclusion, unchanged originals and actual general-file recycling. UI fixture: "+Path.Combine(root,"library.db"));
    }
}
