namespace LocalPhotoAI.Shared.Models;

public class UploadResponse
{
    public List<UploadedFileInfo> Files { get; set; } = [];
}

public class UploadedFileInfo
{
    public string PhotoId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
}
