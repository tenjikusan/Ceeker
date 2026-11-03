using System.Text.Json;
using Wormy.Core;

internal static class UpdateChecks
{
    public static void Run()
    {
        var release = new ReleaseUpdate("0.26.0-preview.2", "https://example.com/setup.exe", new string('A',64), "Release notes");
        if (!release.IsNewerThan("0.26.0-preview.1") || release.IsNewerThan("0.26.0") || release.IsNewerThan("0.27.0-preview.1") || release.IsNewerThan(release.Version)) throw new Exception("Release ordering failed.");
        if (!(release with { Version="0.26.0" }).IsNewerThan("0.26.0-preview.99")) throw new Exception("Stable did not supersede preview.");
        if (!release.IsNewerThan("0.9.0")) throw new Exception("Versions sorted lexically.");
        if (ReleaseUpdate.Parse(JsonSerializer.Serialize(release)) != release) throw new Exception("Manifest did not round-trip.");
        foreach (var uri in new[] { "http://example.com/x", "file:///C:/test.exe", "https://user:password@example.com/x", "https://localhost/x" })
        {
            try { ReleaseUpdate.Https(uri); throw new Exception("Unsafe URL accepted."); } catch (InvalidDataException) { }
        }
        foreach (var bad in new[] { release with { Version="garbage" }, release with { Sha256="bad" }, release with { Notes=new string('x',8001) } })
        {
            try { ReleaseUpdate.Parse(JsonSerializer.Serialize(bad)); throw new Exception("Invalid manifest accepted."); } catch (InvalidDataException) { }
        }
        try { ReleaseUpdate.Parse(new string('x',65537)); throw new Exception("Oversized manifest accepted."); } catch (InvalidDataException) { }
        Console.WriteLine("PASS: release version ordering, stable/preview boundaries, manifest round-trip, URL and payload validation.");
    }
}
