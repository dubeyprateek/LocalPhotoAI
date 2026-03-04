namespace LocalPhotoAI.Shared.Queue;

public class QueueMessage
{
    public string JobId { get; set; } = string.Empty;
    public string PhotoId { get; set; } = string.Empty;
    public string Pipeline { get; set; } = "default";
    public string? SessionId { get; set; }
    public string? Prompt { get; set; }
}
