using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wormy.Core;

public sealed record ReleaseUpdate(string Version, string InstallerUrl, string Sha256, string Notes)
{
    public static Uri Https(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.HostNameType != UriHostNameType.Dns || uri.IsLoopback)
            throw new InvalidDataException("Use an HTTPS website URL without credentials.");
        return uri;
    }
    private static (Version Number, int? Preview) ParseVersion(string value)
    {
        var match = Regex.Match(value, @"^(\d+)\.(\d+)\.(\d+)(?:-preview\.(\d+))?$", RegexOptions.CultureInvariant);
        if (!match.Success || !System.Version.TryParse(string.Join('.', match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value), out var version))
            throw new InvalidDataException("Unsupported release version.");
        int? preview = null;
        if (match.Groups[4].Success) { if (!int.TryParse(match.Groups[4].Value, out var number)) throw new InvalidDataException("Invalid preview number."); preview = number; }
        return (version, preview);
    }
    public bool IsNewerThan(string installed)
    {
        var next = ParseVersion(Version); var current = ParseVersion(installed);
        var comparison = next.Number.CompareTo(current.Number);
        if (comparison != 0) return comparison > 0;
        return next.Preview is null ? current.Preview is not null : current.Preview is not null && next.Preview > current.Preview;
    }
    public static ReleaseUpdate Parse(string json)
    {
        if (json.Length > 65536) throw new InvalidDataException("Release manifest is too large.");
        var release = JsonSerializer.Deserialize<ReleaseUpdate>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty release manifest.");
        if (release.Version is null || release.InstallerUrl is null || release.Sha256 is null || release.Notes is null) throw new InvalidDataException("Incomplete release manifest.");
        _ = ParseVersion(release.Version); _ = Https(release.InstallerUrl);
        if (!Regex.IsMatch(release.Sha256, "^[a-fA-F0-9]{64}$") || release.Notes.Length > 8000) throw new InvalidDataException("Invalid release checksum or notes.");
        return release;
    }
}
