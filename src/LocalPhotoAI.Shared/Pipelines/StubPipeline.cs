namespace LocalPhotoAI.Shared.Pipelines;

/// <summary>
/// Stub pipeline that copies the original file as-is.
/// Used for end-to-end testing without a real AI dependency.
/// </summary>
public class StubPipeline : IImagePipeline
{
    public string Name => "stub";

    public async Task<PipelineResult> RunAsync(string inputPath, string outputDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, Path.GetFileName(inputPath));

        using var sourceStream = File.OpenRead(inputPath);
        using var destStream = File.Create(outputPath);
        await sourceStream.CopyToAsync(destStream, cancellationToken);

        return new PipelineResult
        {
            Success = true,
            OutputPath = outputPath
        };
    }
}
