using Windows.Storage;
using Wormy.Core;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly Dictionary<string, (long Length, DateTime Modified, string? Model)> deviceModels = new(StringComparer.OrdinalIgnoreCase);
    private async Task<IReadOnlyList<FileEntry>> DeviceSearchSnapshotAsync(IReadOnlyList<FileEntry> snapshot, SearchQuery query, CancellationToken token)
    {
        if (query.Devices.Count == 0) return snapshot;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(10));
        var result = new List<FileEntry>();
        foreach (var file in snapshot.Where(f => f.Category is "Images" or "Videos").Take(1000))
        {
            if (budget.IsCancellationRequested) break;
            token.ThrowIfCancellationRequested(); string? model = null;
            try
            {
                ThumbnailCache.Validate(file);
                if (deviceModels.TryGetValue(file.Path, out var cached) && cached.Length == file.Length && cached.Modified == file.ModifiedUtc) model = cached.Model;
                else
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(budget.Token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    var storage = await StorageFile.GetFileFromPathAsync(file.Path).AsTask(timeout.Token);
                    var properties = await storage.Properties.RetrievePropertiesAsync(new[] { "System.Photo.CameraManufacturer", "System.Photo.CameraModel", "System.Devices.ModelName" }).AsTask(timeout.Token);
                    model = string.Join(" ", properties.Values.OfType<string>().Where(v => !string.IsNullOrWhiteSpace(v)));
                    if (deviceModels.Count >= 2000) deviceModels.Clear();
                    deviceModels[file.Path] = (file.Length, file.ModifiedUtc, model);
                }
                ThumbnailCache.Validate(file);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { }
            result.Add(file with { DeviceOrigin = model });
        }
        return result;
    }
}
