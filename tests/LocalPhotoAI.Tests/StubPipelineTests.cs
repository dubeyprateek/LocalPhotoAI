using LocalPhotoAI.Shared.Pipelines;

namespace LocalPhotoAI.Tests;

public class StubPipelineTests
{
    [Fact]
    public async Task RunAsync_CopiesFileToOutputDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "stubpipeline_test_" + Guid.NewGuid().ToString("N"));
        var inputDir = Path.Combine(tempDir, "input");
        var outputDir = Path.Combine(tempDir, "output");
        Directory.CreateDirectory(inputDir);

        try
        {
            var inputPath = Path.Combine(inputDir, "test.jpg");
            var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
            await File.WriteAllBytesAsync(inputPath, content);

            var pipeline = new StubPipeline();
            Assert.Equal("stub", pipeline.Name);

            var result = await pipeline.RunAsync(inputPath, outputDir);

            Assert.True(result.Success);
            Assert.True(File.Exists(result.OutputPath));
            Assert.Equal(Path.Combine(outputDir, "test.jpg"), result.OutputPath);

            var outputContent = await File.ReadAllBytesAsync(result.OutputPath);
            Assert.Equal(content, outputContent);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_CreatesOutputDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "stubpipeline_test_" + Guid.NewGuid().ToString("N"));
        var inputDir = Path.Combine(tempDir, "input");
        var outputDir = Path.Combine(tempDir, "output", "nested", "deep");
        Directory.CreateDirectory(inputDir);

        try
        {
            var inputPath = Path.Combine(inputDir, "photo.png");
            await File.WriteAllBytesAsync(inputPath, [0x89, 0x50, 0x4E, 0x47]);

            var pipeline = new StubPipeline();
            var result = await pipeline.RunAsync(inputPath, outputDir);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(outputDir));
            Assert.Equal(".png", Path.GetExtension(result.OutputPath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
