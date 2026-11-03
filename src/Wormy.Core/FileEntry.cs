namespace Wormy.Core;

public sealed record FileEntry(string Path, string Name, long Length, DateTime ModifiedUtc)
{
    public string? DownloadSource { get; init; }
    public string? SourceHost { get; init; }
    public string? DeviceOrigin { get; init; }
    public DateTime? CreatedUtc { get; init; }
    public DateTime? AccessedUtc { get; init; }
    public DateTime? AddedUtc { get; init; }
    public bool CaptureMetadata { get; init; }
    public string Extension => System.IO.Path.GetExtension(Name).ToLowerInvariant();
    public string Category => Extension switch
    {
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".heic" or ".heif" or ".svg" or ".tif" or ".tiff" => "Images",
        ".mp4" or ".mov" or ".mkv" or ".avi" or ".webm" or ".wmv" => "Videos",
        ".mp3" or ".wav" or ".flac" or ".m4a" or ".aac" or ".ogg" => "Music",
        ".pdf" or ".txt" or ".md" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".csv" or ".ppt" or ".pptx" or ".rtf" => "Documents",
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "Archives",
        _ => "Other"
    };
    public string DisplaySize => Length >= 1_000_000_000 ? $"{Length / 1_000_000_000d:F1} GB"
        : Length >= 1_000_000 ? $"{Length / 1_000_000d:F1} MB" : Length >= 1_000 ? $"{Length / 1_000d:F1} KB" : $"{Length} B";
}
