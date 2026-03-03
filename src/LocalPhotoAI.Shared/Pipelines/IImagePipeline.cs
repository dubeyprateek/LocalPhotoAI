namespace LocalPhotoAI.Shared.Pipelines;

public class PipelineResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public interface IImagePipeline
{
    string Name { get; }
    Task<PipelineResult> RunAsync(string inputPath, string outputDir, CancellationToken cancellationToken = default);
}
