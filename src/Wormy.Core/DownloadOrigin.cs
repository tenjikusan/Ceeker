namespace Wormy.Core;

public sealed record DownloadOrigin(string Source, string Host);

public static class DownloadOrigins
{
    private static readonly (string Domain, string Name)[] Known = [
        ("mail.google.com", "Gmail"), ("drive.google.com", "Google Drive"), ("docs.google.com", "Google Drive"),
        ("chatgpt.com", "ChatGPT"), ("chat.openai.com", "ChatGPT"), ("github.com", "GitHub"),
        ("githubusercontent.com", "GitHub"), ("whatsapp.com", "WhatsApp"), ("slack.com", "Slack"),
        ("figma.com", "Figma"), ("canva.com", "Canva"), ("dropbox.com", "Dropbox"),
        ("notion.so", "Notion"), ("notion.site", "Notion"), ("discord.com", "Discord"),
        ("telegram.org", "Telegram"), ("t.me", "Telegram"), ("outlook.com", "Outlook"),
        ("onedrive.live.com", "OneDrive"), ("sharepoint.com", "Microsoft 365"),
        ("x.com", "X"), ("twitter.com", "X"), ("youtube.com", "YouTube"), ("reddit.com", "Reddit")];

    public static DownloadOrigin? Parse(string text)
    {
        if (text.Length > 16_384) return null;
        string? hostUrl = null, referrer = null;
        var section = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.StartsWith('[')) { section = line.Equals("[ZoneTransfer]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!section || line.StartsWith(';') || !line.Contains('=')) continue;
            var equal = line.IndexOf('='); var key = line[..equal].Trim(); var value = line[(equal + 1)..].Trim();
            if (key.Equals("HostUrl", StringComparison.OrdinalIgnoreCase)) { if (hostUrl is not null && hostUrl != value) return null; hostUrl = value; }
            if (key.Equals("ReferrerUrl", StringComparison.OrdinalIgnoreCase)) { if (referrer is not null && referrer != value) return null; referrer = value; }
        }
        var hosts = new[] { Host(hostUrl), Host(referrer) }.Where(h => h is not null).Cast<string>().ToArray();
        foreach (var host in hosts)
            foreach (var rule in Known)
                if (host.Equals(rule.Domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + rule.Domain, StringComparison.OrdinalIgnoreCase)) return new(rule.Name, host);
        return hosts.Length == 0 ? null : new(hosts[0], hosts[0]);
    }

    private static string? Host(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.IdnHost) && uri.IdnHost.Length <= 253 ? uri.IdnHost.TrimEnd('.').ToLowerInvariant() : null;

    public static DownloadOrigin? Read(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var stream = new FileStream(path + ":Zone.Identifier", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 16_384) return null;
            using var reader = new StreamReader(stream);
            var buffer = new char[16_385]; var length = reader.ReadBlock(buffer, 0, buffer.Length);
            return length > 16_384 ? null : Parse(new string(buffer, 0, length));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { return null; }
    }
}
