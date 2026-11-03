using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Wormy.Core;

namespace Wormy.App;

public static class FileTransfer
{
    public static DataPackage Create(IEnumerable<FileEntry> files, Action<FileEntry> validate, Action<string> reportError)
    {
        var data = new DataPackage(); Configure(data, files, validate, reportError); return data;
    }
    public static void Configure(DataPackage data, IEnumerable<FileEntry> files, Action<FileEntry> validate, Action<string> reportError)
    {
        var entries = files.ToArray();
        data.RequestedOperation = DataPackageOperation.Copy;
        data.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var items = new List<IStorageItem>();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                foreach (var file in entries)
                {
                    await Task.Run(() => validate(file), timeout.Token).WaitAsync(timeout.Token);
                    items.Add(await StorageFile.GetFileFromPathAsync(file.Path).AsTask(timeout.Token));
                    await Task.Run(() => validate(file), timeout.Token).WaitAsync(timeout.Token);
                }
                request.SetData(items.ToArray());
            }
            catch (Exception ex) { reportError(ex.Message); }
            finally { deferral.Complete(); }
        });
    }
}
