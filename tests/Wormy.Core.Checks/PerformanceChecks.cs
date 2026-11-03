using System.Diagnostics;
using System.Text.Json;
using Wormy.Core;
using Wormy.Infrastructure;

internal static class PerformanceChecks
{
    public static async Task RunAsync(string reportPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "Wormy-performance-" + Guid.NewGuid());
        var data = Path.Combine(root, "files");
        Directory.CreateDirectory(data);
        var measurements = new Dictionary<string, double>();
        try
        {
            for (var directory = 0; directory < 100; directory++)
            {
                var folder = Path.Combine(data, directory.ToString("D3"));
                Directory.CreateDirectory(folder);
                for (var file = 0; file < 100; file++)
                    await File.WriteAllTextAsync(Path.Combine(folder, $"invoice-{file:D3}.txt"), $"Generated fixture {directory:D3}-{file / 2:D3}");
            }
            var watch = Stopwatch.StartNew();
            var scan = await FolderScanner.ScanAsync(data, CancellationToken.None);
            measurements["scanMs"] = watch.Elapsed.TotalMilliseconds;
            if (scan.Files.Count != 10000 || scan.Errors.Count != 0) throw new Exception("Large scan lost files.");
            var database = Path.Combine(root, "library.db");
            var index = new LibraryIndex(database);
            watch.Restart();
            await index.ReplaceRootAsync(data, scan.Files, CancellationToken.None);
            measurements["indexWriteMs"] = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var snapshot = await new LibraryIndex(database).LoadAsync();
            measurements["restartLoadMs"] = watch.Elapsed.TotalMilliseconds;
            if (snapshot.Files.Count != 10000 || snapshot.Roots.Count != 1) throw new Exception("Large restart lost records.");
            var query = SearchParser.Parse("txt under 1 KB invoice");
            var samples = new List<double>();
            for (var iteration = 0; iteration < 30; iteration++)
            {
                watch.Restart();
                var matches = await Task.Run(() => snapshot.Files.Where(query.Matches).OrderByDescending(file => file.ModifiedUtc).ToArray());
                samples.Add(watch.Elapsed.TotalMilliseconds);
                if (matches.Length != 10000) throw new Exception("Large search lost matches.");
            }
            measurements["searchMedianMs"] = samples.Order().ElementAt(15);
            measurements["searchMaxMs"] = samples.Max();
            watch.Restart();
            var duplicates = await DuplicateScanner.ScanAsync(snapshot.Files, null, CancellationToken.None);
            measurements["duplicateScanMs"] = watch.Elapsed.TotalMilliseconds;
            if (duplicates.Groups.Count != 5000 || duplicates.Groups.Any(group => group.Files.Count != 2) || duplicates.Skipped.Count != 0)
                throw new Exception("Large duplicate scan grouped incorrectly.");
            measurements["peakWorkingSetMB"] = Process.GetCurrentProcess().PeakWorkingSet64 / 1_000_000d;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
            {
                testedUtc = DateTime.UtcNow, fileCount = 10000, generatedFileBytes = scan.Files.Sum(file => file.Length),
                runtime = Environment.Version.ToString(), measurements,
                scope = "Backend timings on generated small text files. Excludes UI rendering, search debounce, and cold-drive startup. No real user library tested."
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS: 10,000-file scan, index persistence, 30 searches, and 5,000 exact duplicate pairs.");
            foreach (var measurement in measurements) Console.WriteLine($"{measurement.Key}: {measurement.Value:F1}");
        }
        finally
        {
            // This unique directory contains only the generated benchmark fixture and its database.
            Directory.Delete(root, recursive: true);
        }
    }
}
