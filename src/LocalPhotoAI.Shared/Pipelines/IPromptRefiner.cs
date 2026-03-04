namespace LocalPhotoAI.Shared.Pipelines;

public class PromptRefineResult
{
    public string RefinedPrompt { get; set; } = string.Empty;
    public string GeneratedTitle { get; set; } = string.Empty;
}

/// <summary>
/// Refines a user's natural-language prompt into a structured prompt
/// that AI image-processing systems can understand, and generates
/// a short title for the session folder.
/// </summary>
public interface IPromptRefiner
{
    Task<PromptRefineResult> RefineAsync(string userPrompt, CancellationToken cancellationToken = default);
}
