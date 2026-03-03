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

        var extension = Path.GetExtension(inputPath);
        var outputPath = Path.Combine(outputDir, $"edited{extension}");

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
