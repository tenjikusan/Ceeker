using System.Drawing;
using System.Drawing.Imaging;
using Windows.Storage;
internal static class DeviceFixture
{
    public static async Task CreateAsync(string root)
    {
        var path=Path.Combine(Path.GetFullPath(root),"phone.jpg");
        if(File.Exists(path)) throw new IOException("Use a new fixture filename.");
        using(var bitmap=new Bitmap(200,120)) using(var graphics=Graphics.FromImage(bitmap)) { graphics.Clear(Color.CornflowerBlue); bitmap.Save(path,ImageFormat.Jpeg); }
        var file=await StorageFile.GetFileFromPathAsync(path);
        await file.Properties.SavePropertiesAsync(new Dictionary<string,object> { ["System.Photo.CameraModel"]="iPhone 15", ["System.Photo.CameraManufacturer"]="Apple" });
        var saved=await file.Properties.RetrievePropertiesAsync(new[]{"System.Photo.CameraModel"});
        if(saved["System.Photo.CameraModel"] as string != "iPhone 15") throw new Exception("Camera metadata fixture did not persist.");
        File.SetLastWriteTimeUtc(path,new DateTime(2026,8,15,12,0,0,DateTimeKind.Utc));
        Console.WriteLine("PASS: generated JPEG camera metadata persisted: "+path);
    }
}
