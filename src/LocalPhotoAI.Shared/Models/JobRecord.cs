namespace LocalPhotoAI.Shared.Models;

public class JobRecord
{
    public string JobId { get; set; } = string.Empty;
    public string PhotoId { get; set; } = string.Empty;
    public string Pipeline { get; set; } = "default";
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public double Progress { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum JobStatus
{
    Queued,
    Processing,
    Succeeded,
    Failed
}
