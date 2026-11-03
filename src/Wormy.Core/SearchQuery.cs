using System.Globalization;
using System.Text.RegularExpressions;

namespace Wormy.Core;

public sealed class SearchQuery
{
    public HashSet<string> Categories { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Extensions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Terms { get; } = [];
    public List<string> Chips { get; } = [];
    public List<string> Warnings { get; } = [];
    public decimal? MinimumBytes { get; set; }
    public decimal? MaximumBytes { get; set; }
    public bool MinimumInclusive { get; set; }
    public bool MaximumInclusive { get; set; }
    public DateTime? ModifiedFromUtc { get; set; }
    public DateTime? ModifiedUntilUtc { get; set; }

    public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Folders { get; } = [];
    public List<string> Devices { get; } = [];
    public bool ScreenshotsOnly { get; set; }
    public string DateField { get; set; } = "Modified";
    private DateTime? DateOf(FileEntry file) => DateField switch { "Created" => file.CreatedUtc, "Added" => file.AddedUtc, "Accessed" => file.AccessedUtc, _ => file.ModifiedUtc };
    public bool Matches(FileEntry file) => MatchesMetadata(file) &&
        Terms.All(term => file.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

    public bool MatchesMetadata(FileEntry file) =>
        Folders.All(folder => (System.IO.Path.GetDirectoryName(file.Path) ?? "").Contains(folder, StringComparison.OrdinalIgnoreCase)) &&
        (Devices.Count == 0 || file.DeviceOrigin is not null && Devices.Any(device => file.DeviceOrigin.Contains(device, StringComparison.OrdinalIgnoreCase))) &&
        (!ScreenshotsOnly || LibraryInsights.IsScreenshot(file)) &&
        (Sources.Count == 0 || file.DownloadSource is not null && Sources.Contains(file.DownloadSource) || file.SourceHost is not null && Sources.Any(source=>file.SourceHost.Equals(source,StringComparison.OrdinalIgnoreCase) || file.SourceHost.EndsWith("."+source,StringComparison.OrdinalIgnoreCase))) &&
        (Categories.Count + Extensions.Count == 0 || Categories.Contains(file.Category) || Extensions.Contains(file.Extension)) &&
        (MinimumBytes is null || (MinimumInclusive ? file.Length >= MinimumBytes : file.Length > MinimumBytes)) &&
        (MaximumBytes is null || (MaximumInclusive ? file.Length <= MaximumBytes : file.Length < MaximumBytes)) &&
        (ModifiedFromUtc is null || DateOf(file) is { } from && from >= ModifiedFromUtc) &&
        (ModifiedUntilUtc is null || DateOf(file) is { } until && until < ModifiedUntilUtc);
}

/// Explicit, offline grammar. Unrecognized text remains a filename term.
public static class SearchParser
{
    private static readonly RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Dictionary<string,string[]> TypeGroups=new(StringComparer.OrdinalIgnoreCase)
    {
        ["word"]=[".doc",".docx"], ["doc"]=[".doc",".docx",".pages"], ["text"]=[".txt",".md",".rtf"], ["note"]=[".txt",".md",".rtf"],
        ["excel"]=[".xls",".xlsx"], ["spreadsheet"]=[".xls",".xlsx",".csv",".numbers"], ["sheet"]=[".xls",".xlsx",".csv",".numbers"],
        ["presentation"]=[".ppt",".pptx",".key"], ["slide"]=[".ppt",".pptx",".key"], ["deck"]=[".ppt",".pptx",".key"], ["powerpoint"]=[".ppt",".pptx"],
        ["design"]=[".fig",".sketch",".psd",".ai",".svg"], ["installer"]=[".msi",".msix",".msixbundle",".appx",".appxbundle",".exe",".dmg",".pkg"]
    };
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = ".pdf", ["pdfs"] = ".pdf", ["png"] = ".png", ["jpg"] = ".jpg", ["jpeg"] = ".jpeg",
        ["docx"] = ".docx", ["txt"] = ".txt", ["zip"] = ".zip", ["zips"] = ".zip", ["mp4"] = ".mp4",
        ["heic"] = ".heic", ["heif"] = ".heif", ["webp"] = ".webp", ["xlsx"] = ".xlsx", ["pptx"] = ".pptx", ["csv"] = ".csv", ["audio"] = "Music", ["picture"] = "Images", ["pictures"] = "Images",
        ["image"] = "Images", ["images"] = "Images", ["photo"] = "Images", ["photos"] = "Images",
        ["video"] = "Videos", ["videos"] = "Videos", ["music"] = "Music", ["song"] = "Music", ["songs"] = "Music",
        ["document"] = "Documents", ["documents"] = "Documents", ["archive"] = "Archives", ["archives"] = "Archives"
    };

    public static SearchQuery Parse(string input, DateTimeOffset? now = null, TimeZoneInfo? timeZone = null)
    {
        var query = new SearchQuery();
        var zone = timeZone ?? TimeZoneInfo.Local;
        var today = TimeZoneInfo.ConvertTime(now ?? DateTimeOffset.Now, zone).Date;
        var quoted = new List<string>();
        var text = Regex.Replace(input, "\"([^\"]*)\"", match =>
        {
            quoted.Add(match.Groups[1].Value); return $" __quoted{quoted.Count - 1}__ ";
        });
        var unsupportedDate = Regex.IsMatch(text, @"\b(downloaded|received)\b", Options);
        text=Regex.Replace(text,@"\b(one|two|three|four|five|six|seven|eight|nine|ten)\s+(?=(?:days?|weeks?|months?|years?|kb|mb|gb|tb)\b)", m => (Array.IndexOf(new[]{"one","two","three","four","five","six","seven","eight","nine","ten"},m.Groups[1].Value.ToLowerInvariant())+1).ToString(CultureInfo.InvariantCulture)+" ",Options);
        text = Regex.Replace(text, @"\b(?:saved|updated)\s+(?=(?:(?:last|this)\s+(?:week|month)|today|yesterday)\b)", "modified ", Options);
        text = Regex.Replace(text,@"\b(created|added|accessed)\b",match => { query.DateField = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(match.Value.ToLowerInvariant()); return "modified"; },Options);
        text=Regex.Replace(text,@"\b(?:in|inside|from)\s+(?:my\s+|the\s+)?(downloads|desktop|documents|pictures|movies|music)\b",match=>{query.Folders.Add(match.Groups[1].Value);query.Chips.Add("Folder: "+match.Groups[1].Value);return " ";},Options);
        text = Regex.Replace(text, @"\b(?:in|inside|under)\s+(?:the\s+)?folder\s+(__quoted\d+__|[^\s]+)", match =>
        {
            var folder = match.Groups[1].Value;
            if (folder.StartsWith("__quoted") && int.TryParse(folder[8..].TrimEnd('_'), out var position) && position < quoted.Count) folder = quoted[position];
            if (string.IsNullOrWhiteSpace(folder)) return match.Value;
            query.Folders.Add(folder); query.Chips.Add("Folder path contains: " + folder); return " ";
        }, Options);
        text = Regex.Replace(text, @"\bfrom\s+(?:my\s+)?(iphone|ipad|android|pixel|samsung|canon|nikon|sony)\b", match =>
        { query.Devices.Add(match.Groups[1].Value); query.Chips.Add("Recorded device: " + match.Groups[1].Value); return " "; }, Options);
        if (query.Devices.Count > 0) query.Warnings.Add("Device matches use recorded camera/device metadata, not an inferred import source. Files without it are excluded; checking is limited to 1,000 media files or 10 seconds per search.");
        text = Regex.Replace(text, @"\bscreenshots?\b", _ => { query.ScreenshotsOnly = true; query.Chips.Add("Screenshots (name or PNG capture metadata)"); return " "; }, Options);
        var sourceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["github"] = "GitHub", ["gmail"] = "Gmail", ["google drive"] = "Google Drive", ["chatgpt"] = "ChatGPT", ["whatsapp"] = "WhatsApp", ["slack"] = "Slack", ["figma"] = "Figma", ["canva"] = "Canva", ["dropbox"] = "Dropbox", ["notion"] = "Notion", ["discord"] = "Discord", ["telegram"] = "Telegram", ["outlook"] = "Outlook", ["onedrive"] = "OneDrive", ["microsoft 365"] = "Microsoft 365", ["x"] = "X", ["twitter"] = "X", ["youtube"] = "YouTube", ["reddit"] = "Reddit" };
        text = Regex.Replace(text, @"\bfrom\s+(?:my\s+)?(google drive|microsoft 365|github|gmail|chatgpt|whatsapp|slack|figma|canva|dropbox|notion|discord|telegram|outlook|onedrive|twitter|youtube|reddit|x)(?![\w.-])", match => { query.Sources.Add(sourceNames[match.Groups[1].Value]); return " "; }, Options);
        if (query.Sources.Count > 0) query.Chips.Add("From: " + string.Join(" or ", query.Sources));
        text=Regex.Replace(text,@"\bfrom\s+((?:[a-z0-9-]+\.)+[a-z]{2,63})(?![\w.-])",match=>{var source=match.Groups[1].Value.ToLowerInvariant();query.Sources.Add(source);query.Chips.Add("From: "+source);return " ";},Options);

        text = Regex.Replace(text, @"(?<![\w.])(at least|at most|over|under|above|below|larger than|bigger than|smaller than|greater than|less than|>=|<=|>|<)\s*(\d+(?:\.\d+)?)\s*(bytes?|kb|mb|gb|tb|kib|mib|gib|tib)\b", match =>
        {
            if (!decimal.TryParse(match.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            { query.Warnings.Add("Size is outside the supported range."); return match.Value; }
            var unit = match.Groups[3].Value.ToLowerInvariant();
            var power = unit[0] switch { 'k' => 1, 'm' => 2, 'g' => 3, 't' => 4, _ => 0 };
            decimal multiplier = 1;
            for (var i = 0; i < power; i++) multiplier *= unit.Contains('i') ? 1024 : 1000;
            if (value > long.MaxValue / multiplier)
            { query.Warnings.Add("Size is outside the supported range."); return match.Value; }
            var bytes = value * multiplier;
            var comparator = match.Groups[1].Value.ToLowerInvariant();
            var upper = comparator is "at most" or "under" or "below" or "smaller than" or "less than" or "<" or "<=";
            var inclusive = comparator is "at least" or "at most" or ">=" or "<=";
            if (upper)
            {
                if (query.MaximumBytes is null || bytes < query.MaximumBytes) { query.MaximumBytes = bytes; query.MaximumInclusive = inclusive; }
                else if (bytes == query.MaximumBytes) query.MaximumInclusive &= inclusive;
            }
            else
            {
                if (query.MinimumBytes is null || bytes > query.MinimumBytes) { query.MinimumBytes = bytes; query.MinimumInclusive = inclusive; }
                else if (bytes == query.MinimumBytes) query.MinimumInclusive &= inclusive;
            }
            query.Chips.Add($"Size {comparator} {value.ToString(CultureInfo.InvariantCulture)} {unit.ToUpperInvariant()}");
            return " ";
        }, Options);

        DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
        if (text.Length>0)
        {
            void AddRange(DateTime? start, DateTime? end)
            {
                if (start is { } from && (query.ModifiedFromUtc is null || from > query.ModifiedFromUtc)) query.ModifiedFromUtc = from;
                if (end is { } until && (query.ModifiedUntilUtc is null || until < query.ModifiedUntilUtc)) query.ModifiedUntilUtc = until;
            }
            text = Regex.Replace(text, @"\b(?:modified\s+)?(?:on\s+|last\s+)?(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", match =>
            {
                var day = Enum.Parse<DayOfWeek>(match.Groups[1].Value,true);
                var offset = ((int)today.DayOfWeek - (int)day + 7) % 7;
                if (match.Value.StartsWith("last",StringComparison.OrdinalIgnoreCase) && offset == 0) offset = 7;
                var date = today.AddDays(-offset); AddRange(Utc(date),Utc(date.AddDays(1)));
                query.Chips.Add($"Modified on {date:yyyy-MM-dd} · {zone.DisplayName}"); return " ";
            }, Options);
            text = Regex.Replace(text, @"\b(?:in\s+the\s+)?(?:last|past)\s+(\d+)\s+(weeks?|months?|years?)\b", match =>
            {
                if (!int.TryParse(match.Groups[1].Value,out var count) || count is < 1 or > 100) { query.Warnings.Add("Relative ranges support 1–100 units."); return match.Value; }
                try
                {
                    var unit = match.Groups[2].Value.ToLowerInvariant();
                    var end = today.AddDays(1);
                    var start = unit.StartsWith("week") ? end.AddDays(-7*count) : unit.StartsWith("month") ? end.AddMonths(-count) : end.AddYears(-count);
                    AddRange(Utc(start),Utc(end)); query.Chips.Add($"Modified in {match.Value} · {zone.DisplayName}"); return " ";
                }
                catch (ArgumentOutOfRangeException) { query.Warnings.Add("Date range is outside supported dates."); return match.Value; }
            },Options);
            text = Regex.Replace(text, @"\b(?:in|during|from)\s+(january|february|march|april|may|june|july|august|september|october|november|december)(?:\s+(\d{4}))?\b", match =>
            {
                var monthNumber = Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames, name => name.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) + 1;
                var year = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : today.Year - (monthNumber > today.Month ? 1 : 0);
                if (year is < 2 or > 9998) { query.Warnings.Add("Month years must be 0002–9998."); return match.Value; }
                var start = new DateTime(year, monthNumber, 1); AddRange(Utc(start), Utc(start.AddMonths(1)));
                query.Chips.Add($"Modified in {start:MMMM yyyy} · {zone.DisplayName}"); return " ";
            }, Options);
            text = Regex.Replace(text, @"\bolder than\s+(\d+)\s+(days?|months?|years?)\b", match =>
            {
                if (!int.TryParse(match.Groups[1].Value, out var count) || count is < 1 or > 3650) { query.Warnings.Add("Age supports 1–3650 units."); return match.Value; }
                try { var cutoff = match.Groups[2].Value.StartsWith("month", StringComparison.OrdinalIgnoreCase) ? today.AddMonths(-count) : match.Groups[2].Value.StartsWith("year", StringComparison.OrdinalIgnoreCase) ? today.AddYears(-count) : today.AddDays(-count); AddRange(null, Utc(cutoff)); }
                catch (ArgumentOutOfRangeException) { query.Warnings.Add("Age is outside the supported date range."); return match.Value; }
                query.Chips.Add("Modified " + match.Value); return " ";
            }, Options);
            text = Regex.Replace(text, @"\b(?:modified\s+)?(on|before|after|since)\s+(\d{4}-\d{2}-\d{2})\b", match =>
            {
                if (!DateTime.TryParseExact(match.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year is < 2 or > 9998)
                { query.Warnings.Add("Use a valid modification date in YYYY-MM-DD format (years 0002–9998)."); return match.Value; }
                var comparison = match.Groups[1].Value.ToLowerInvariant();
                try
                {
                    var start = comparison == "after" ? Utc(date.AddDays(1)) : Utc(date);
                    if (comparison == "before") AddRange(null, start);
                    else if (comparison == "on") AddRange(start, Utc(date.AddDays(1)));
                    else AddRange(start, null);
                }
                catch (ArgumentException) { query.Warnings.Add("That local date cannot be converted in this time zone."); return match.Value; }
                query.Chips.Add($"Modified {comparison} {date:yyyy-MM-dd} · {zone.DisplayName}");
                return " ";
            }, Options);
            text = Regex.Replace(text, @"\b(?:modified\s+(?:in\s+)?)?(?:in\s+the\s+)?last\s+(\d+)\s+days?\b", match =>
            {
                if (!int.TryParse(match.Groups[1].Value, out var days) || days is < 1 or > 3650 || today.Year < 12)
                { query.Warnings.Add("Last N days supports 1–3650 calendar days, including today."); return match.Value; }
                try { AddRange(Utc(today.AddDays(1 - days)), Utc(today.AddDays(1))); }
                catch (ArgumentException) { query.Warnings.Add("That local date range cannot be converted in this time zone."); return match.Value; }
                query.Chips.Add($"Modified last {days} calendar days (including today) · {zone.DisplayName}");
                return " ";
            }, Options);
            text = Regex.Replace(text, @"\b(?:modified\s+)?(?:from\s+|in\s+|during\s+)?(today|yesterday|this week|last week|this month|last month)\b", match =>
            {
                var phrase = match.Groups[1].Value.ToLowerInvariant();
                var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                var month = new DateTime(today.Year, today.Month, 1);
                var (start, end) = phrase switch
                {
                    "today" => (today, today.AddDays(1)), "yesterday" => (today.AddDays(-1), today),
                    "this week" => (monday, monday.AddDays(7)), "last week" => (monday.AddDays(-7), monday),
                    "this month" => (month, month.AddMonths(1)), _ => (month.AddMonths(-1), month)
                };
                var from = Utc(start); var until = Utc(end);
                query.ModifiedFromUtc = query.ModifiedFromUtc is null || from > query.ModifiedFromUtc ? from : query.ModifiedFromUtc;
                query.ModifiedUntilUtc = query.ModifiedUntilUtc is null || until < query.ModifiedUntilUtc ? until : query.ModifiedUntilUtc;
                query.Chips.Add($"Modified {phrase} · {zone.DisplayName}");
                return " ";
            }, Options);
        }

        var structured = query.Chips.Count > 0;
        foreach (var token in Regex.Split(text.Trim(), @"\s+"))
        {
            if (token.Length == 0) continue;
            if (token.StartsWith("__quoted") && int.TryParse(token[8..].TrimEnd('_'), out var quotedIndex) && quotedIndex < quoted.Count)
            { if (quoted[quotedIndex].Length > 0) query.Terms.Add(quoted[quotedIndex]); continue; }
            if (Types.TryGetValue(token, out var type) || Types.TryGetValue(token.TrimEnd('s','S'),out type))
            {
                if (type.StartsWith('.')) query.Extensions.Add(type); else query.Categories.Add(type);
                if(type is ".jpg" or ".jpeg") {query.Extensions.Add(".jpg");query.Extensions.Add(".jpeg");}
                structured = true;
                continue;
            }
            if(TypeGroups.TryGetValue(token.TrimEnd('s','S'),out var extensions)) { foreach(var extension in extensions) query.Extensions.Add(extension); structured=true; continue; }
            if(new[]{"pic","pics","movie","movies","clip","clips","recording","recordings","track","tracks","podcast","podcasts","compressed"}.Contains(token.ToLowerInvariant())) { query.Categories.Add(token.StartsWith("pic",StringComparison.OrdinalIgnoreCase)?"Images":token.StartsWith("track",StringComparison.OrdinalIgnoreCase)||token.StartsWith("podcast",StringComparison.OrdinalIgnoreCase)?"Music":token.Equals("compressed",StringComparison.OrdinalIgnoreCase)?"Archives":"Videos");structured=true;continue; }
            if(new[]{"gif","svg","mov","mp3","wav","psd","sketch","dmg","pkg"}.Contains(token.ToLowerInvariant())) { query.Extensions.Add("."+token.ToLowerInvariant());structured=true;continue; }
            query.Terms.Add(token);
        }
        query.Terms.RemoveAll(term => !quoted.Contains(term) && (new[] { "show", "find", "me", "my", "files", "file", "please" }.Contains(term.ToLowerInvariant()) || structured && new[] { "all", "and", "or", "with", "of", "containing", "contains", "about", "that", "have", "a", "an", "the", "any", "some", "i", "which", "were", "was", "is", "are", "for", "to", "get", "stuff", "things", "everything" }.Contains(term.ToLowerInvariant()) || (query.ModifiedFromUtc is not null || query.ModifiedUntilUtc is not null) && new[]{"saved","updated","modified","edited","made"}.Contains(term.ToLowerInvariant())));
        if(unsupportedDate)
        {
            if(query.ModifiedFromUtc is not null || query.ModifiedUntilUtc is not null)
            {
                query.Terms.RemoveAll(term=>!quoted.Contains(term)&&new[]{"downloaded","received"}.Contains(term.ToLowerInvariant()));
                query.Warnings.Add("Downloaded/received phrases use the selected file-date field. Actual download timestamps are not recorded.");
            }
            else query.Warnings.Add("Download/received dates are not recorded. Without a date phrase these words remain filename text.");
        }
        if (query.Categories.Count + query.Extensions.Count > 0)
            query.Chips.Insert(0, "Type: " + string.Join(" or ", query.Categories.Concat(query.Extensions.Select(extension => extension[1..].ToUpperInvariant()))));
        query.Chips.AddRange(query.Terms.Select(term => $"Name contains “{term}”"));
        if (query.MinimumBytes is { } min && query.MaximumBytes is { } max && (min > max || min == max && (!query.MinimumInclusive || !query.MaximumInclusive)))
            query.Warnings.Add("The size filters conflict; no files can match.");
        if (query.ModifiedFromUtc >= query.ModifiedUntilUtc && query.ModifiedFromUtc is not null && query.ModifiedUntilUtc is not null)
            query.Warnings.Add("The date filters conflict; no files can match.");
        if (query.DateField != "Modified")
        {
            for (var i=0;i<query.Chips.Count;i++) query.Chips[i]=query.Chips[i].Replace("Modified",query.DateField);
            query.Warnings.Add(query.DateField == "Added" ? "Added means first recorded by Ceeker, not download time. Older migrated records may have no added date." : "Refresh connected folders to populate this metadata; unknown dates are excluded. Windows access times may be delayed or disabled.");
        }
        return query;
    }
}

