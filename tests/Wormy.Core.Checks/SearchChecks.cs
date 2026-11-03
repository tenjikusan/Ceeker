using Wormy.Core;

static class SearchChecks
{
    public static void Run()
    {
        var now = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test India", TimeSpan.FromHours(5.5), "Test India", "Test India");
        SearchQuery Parse(string text) => SearchParser.Parse(text, now, zone);
        var pdf = new FileEntry("C:/fixture/invoice.pdf", "invoice.pdf", 5_000_000, new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));
        var phone = new FileEntry("C:/fixture/Summer Trip/movie.mp4", "movie.mp4", 10, new DateTime(2026,8,15,0,0,0,DateTimeKind.Utc)) { DeviceOrigin = "Apple iPhone 15" };
        if (!Parse("videos from my iphone in august 2026 inside folder \"Summer Trip\"").Matches(phone) || Parse("videos from my iphone in august 2026 inside folder \"Summer Trip\"").Matches(phone with { DeviceOrigin = null }) || Parse("videos in august 2025").Matches(phone)) throw new Exception("Folder/month/device combined filters failed.");
        if (!Parse("videos older than 1 month").Matches(phone) || Parse("videos in folder Missing").Matches(phone) || Parse("screenshots").Matches(phone)) throw new Exception("Age/folder/screenshot search failed.");
        if (Parse("\"from my iphone\"").Devices.Count != 0 || Parse("\"in august\"").ModifiedFromUtc is not null || Parse("\"screenshots\"").ScreenshotsOnly) throw new Exception("Quoted literal parsed as grammar.");
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        Check(!Parse("PDFs over 5 MB").Matches(pdf), "Strict size boundary failed.");
        Check(Parse("show me PDFs at least 5 MB").Matches(pdf), "Inclusive boundary or filler words failed.");
        Check(Parse("pdfs under 6 MB invoice").Matches(pdf), "Combined filename/type/size search failed.");
        Check(!Parse("images under 6 MB invoice").Matches(pdf), "Type filtering failed.");
        Check(Parse("pdfs at least 4 MiB").Matches(pdf), "Binary size units failed.");
        Check(Parse("pdfs over 4.5 MB under 5.5 MB").Matches(pdf), "Decimal size/range search failed.");
        var yesterday = Parse("pdfs modified yesterday");
        Check(Parse("images saved last week").Terms.Count == 0 && Parse("images saved last week").ModifiedFromUtc is not null, "Saved date phrase left a required filename term.");
        Check(Parse("images updated yesterday").Terms.Count == 0, "Updated date phrase failed.");
        Check(Parse("images \"saved\" last week").Terms.Contains("saved") && Parse("images saved receipt").Terms.Contains("saved"), "Literal saved text was lost.");
        Check(yesterday.Matches(pdf), "Yesterday did not match correct local date.");
        Check(yesterday.Matches(pdf with { ModifiedUtc = new DateTime(2026, 10, 8, 18, 30, 0, DateTimeKind.Utc) }), "Local start boundary excluded.");
        Check(!yesterday.Matches(pdf with { ModifiedUtc = new DateTime(2026, 10, 9, 18, 30, 0, DateTimeKind.Utc) }), "Exclusive end boundary included.");
        Check(Parse("images last week").Matches(pdf with { Name = "photo.jpg", ModifiedUtc = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc) }), "Previous calendar week failed.");
        Check(!Parse("images last week").Matches(pdf with { Name = "photo.jpg" }), "Previous week included current week.");
        Check(Parse("\"pdfs\"").Matches(pdf with { Name = "pdfs notes.txt" }), "Quoted type word became a filter.");
        Check(Parse("pdfs \"files\"").Terms.Contains("files"), "Quoted filler was discarded.");
        Check(Parse("created yesterday").DateField == "Created" && !Parse("created yesterday").Matches(pdf) && Parse("created yesterday").Matches(pdf with { CreatedUtc = pdf.ModifiedUtc }), "Creation-date scope failed.");
        Check(Parse("downloaded yesterday").ModifiedFromUtc is not null && Parse("downloaded yesterday").Terms.Count==0 && Parse("downloaded yesterday").Warnings.Count > 0, "Download date alias lacked an explicit file-date explanation.");
        Check(Parse("pdfs on friday").Matches(pdf) && Parse("pdfs past 2 weeks").Matches(pdf), "Weekday/relative range search failed.");
        Check(Parse("pdfs over 10 MB under 5 MB").Warnings.Count > 0, "Contradictory size filters not explained.");
        Check(Parse("pdfs over 99999999999999999999999 TB").Warnings.Count > 0, "Overflow was not handled.");
        Check(Parse("invoice").Matches(pdf), "Plain filename search regressed.");
        Check(Parse("pdfs modified on 2026-10-09").Matches(pdf), "Explicit local date failed.");
        Check(!Parse("modified before 2026-10-09").Matches(pdf), "Before included boundary day.");
        Check(Parse("modified since 2026-10-09").Matches(pdf), "Since excluded boundary day.");
        Check(!Parse("modified after 2026-10-09").Matches(pdf), "After included boundary day.");
        Check(Parse("modified since 2026-10-08 modified before 2026-10-10").Matches(pdf), "Explicit range failed.");
        Check(Parse("last 2 days").Matches(pdf) && !Parse("last 1 day").Matches(pdf), "Calendar-day range failed.");
        Check(!Parse("last 2 days").Matches(pdf with { ModifiedUtc = new DateTime(2026, 10, 10, 18, 30, 0, DateTimeKind.Utc) }), "Calendar range included tomorrow.");
        Check(Parse("modified on 2026-02-30").Warnings.Count > 0, "Invalid date was silently accepted.");
        Check(Parse("last 0 days").Warnings.Count > 0 && Parse("last 999999999999 days").Warnings.Count > 0, "Invalid day counts were accepted.");
        Check(Parse("modified since 2026-10-10 modified before 2026-10-09").Warnings.Count > 0, "Explicit date conflict was not explained.");
        Check(Parse("\"last 2 days\"").ModifiedFromUtc is null, "Quoted date became a filter.");
        Console.WriteLine("PASS: search types, phrases, decimal/binary sizes, strict/inclusive boundaries, local date ranges, quoted terms, unsupported dates, and conflicts.");
    }
}
