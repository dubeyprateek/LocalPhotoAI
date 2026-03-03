namespace LocalPhotoAI.Shared.Models;

public class PhotoMetadata
{
    public string PhotoId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public List<PhotoVersion> Versions { get; set; } = [];
}

public class PhotoVersion
{
    public string VersionName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
