using System.Text.RegularExpressions;

namespace LocalPhotoAI.Shared.Pipelines;

/// <summary>
/// Stub prompt refiner that formats the user prompt and generates a title.
/// Replace with a real LLM-backed refiner for production use.
/// </summary>
public partial class StubPromptRefiner : IPromptRefiner
{
    public Task<PromptRefineResult> RefineAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        var trimmed = userPrompt.Trim();

        // Generate a short title from the first few words
        var words = NonAlphanumericRegex().Replace(trimmed, " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titleWords = words.Take(4);
        var title = string.Join("-", titleWords).ToLowerInvariant();
        if (string.IsNullOrEmpty(title))
            title = "session";

        // Stub refinement: wrap user intent in a structured format
        var refined = $"Apply the following transformation to each input image: {trimmed}. " +
                      "Preserve original resolution and aspect ratio. Output as PNG.";

        return Task.FromResult(new PromptRefineResult
        {
            RefinedPrompt = refined,
            GeneratedTitle = title
        });
    }

    [GeneratedRegex(@"[^a-zA-Z0-9\s]")]
    private static partial Regex NonAlphanumericRegex();
}
