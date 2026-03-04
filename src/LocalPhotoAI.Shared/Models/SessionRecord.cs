namespace LocalPhotoAI.Shared.Models;

public class SessionRecord
{
    public string SessionId { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public string RefinedPrompt { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string InputFolder { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public SessionStatus Status { get; set; } = SessionStatus.Draft;
    public List<string> PhotoIds { get; set; } = [];
    public List<string> JobIds { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum SessionStatus
{
    Draft,
    Uploading,
    Processing,
    Completed,
    Failed
}
