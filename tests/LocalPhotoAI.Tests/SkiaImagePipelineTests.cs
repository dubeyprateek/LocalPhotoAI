using LocalPhotoAI.Shared.Pipelines;
using SkiaSharp;

namespace LocalPhotoAI.Tests;

public class SkiaImagePipelineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _inputDir;
    private readonly string _outputDir;
    private readonly string _inputPath;

    public SkiaImagePipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "skia_test_" + Guid.NewGuid().ToString("N"));
        _inputDir = Path.Combine(_tempDir, "input");
        _outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(_inputDir);

        // Create a small 4x4 red PNG as test input
        _inputPath = Path.Combine(_inputDir, "test.png");
        using var bitmap = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(_inputPath);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Name_ReturnsSkia()
    {
        var pipeline = new SkiaImagePipeline();
        Assert.Equal("skia", pipeline.Name);
    }

    [Fact]
    public async Task RunAsync_WithNullPrompt_ProducesOutputFile()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(".png", Path.GetExtension(result.OutputPath));
    }

    [Fact]
    public async Task RunAsync_WithGrayscalePrompt_ProducesGrayscaleImage()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "make it grayscale");

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));

        using var output = SKBitmap.Decode(result.OutputPath);
        var pixel = output.GetPixel(0, 0);
        // Grayscale red (0.2126*255 ~ 54): R, G, B should be roughly equal
        Assert.Equal(pixel.Red, pixel.Green);
        Assert.Equal(pixel.Green, pixel.Blue);
    }

    [Fact]
    public async Task RunAsync_WithSepiaPrompt_ChangesColors()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "apply sepia tone");

        Assert.True(result.Success);
        using var output = SKBitmap.Decode(result.OutputPath);
        var pixel = output.GetPixel(0, 0);
        // Sepia shifts colors: R > G > B
        Assert.True(pixel.Red >= pixel.Green);
        Assert.True(pixel.Green >= pixel.Blue);
    }

    [Fact]
    public async Task RunAsync_WithBlurPrompt_Succeeds()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "blur 5");

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task RunAsync_WithResizePrompt_ChangesImageDimensions()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "resize 2x2");

        Assert.True(result.Success);
        using var output = SKBitmap.Decode(result.OutputPath);
        Assert.Equal(2, output.Width);
        Assert.Equal(2, output.Height);
    }

    [Fact]
    public async Task RunAsync_WithRotatePrompt_Succeeds()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "rotate 90");

        Assert.True(result.Success);
        using var output = SKBitmap.Decode(result.OutputPath);
        // 90-degree rotation of a 4x4 image should produce 4x4
        Assert.Equal(4, output.Width);
        Assert.Equal(4, output.Height);
    }

    [Fact]
    public async Task RunAsync_WithInvertPrompt_InvertsColors()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "invert colors");

        Assert.True(result.Success);
        using var output = SKBitmap.Decode(result.OutputPath);
        var pixel = output.GetPixel(0, 0);
        // Inverted red (255,0,0) should become cyan (0,255,255)
        Assert.True(pixel.Red < 10);
        Assert.True(pixel.Green > 245);
        Assert.True(pixel.Blue > 245);
    }

    [Fact]
    public async Task RunAsync_WithFlipHorizontalPrompt_Succeeds()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "flip horizontal");

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task RunAsync_WithMultipleOperations_AppliesAll()
    {
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, _outputDir, "grayscale and sharpen");

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task RunAsync_WithInvalidFile_ReturnsFailed()
    {
        var badPath = Path.Combine(_inputDir, "bad.jpg");
        await File.WriteAllBytesAsync(badPath, [0x00, 0x01, 0x02]);

        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(badPath, _outputDir, "grayscale");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task RunAsync_CreatesOutputDirectory()
    {
        var deepOutput = Path.Combine(_outputDir, "nested", "deep");
        var pipeline = new SkiaImagePipeline();
        var result = await pipeline.RunAsync(_inputPath, deepOutput, "grayscale");

        Assert.True(result.Success);
        Assert.True(Directory.Exists(deepOutput));
    }

    // -- ParseOperations unit tests ------------------------------------------

    [Fact]
    public void ParseOperations_NullPrompt_DefaultsToGrayscale()
    {
        var ops = SkiaImagePipeline.ParseOperations(null);
        Assert.Single(ops);
        Assert.Equal(SkiaImagePipeline.OperationType.Grayscale, ops[0].Type);
    }

    [Fact]
    public void ParseOperations_EmptyPrompt_DefaultsToGrayscale()
    {
        var ops = SkiaImagePipeline.ParseOperations("  ");
        Assert.Single(ops);
        Assert.Equal(SkiaImagePipeline.OperationType.Grayscale, ops[0].Type);
    }

    [Fact]
    public void ParseOperations_UnrecognizedPrompt_DefaultsToGrayscale()
    {
        var ops = SkiaImagePipeline.ParseOperations("make it look like a painting");
        Assert.Single(ops);
        Assert.Equal(SkiaImagePipeline.OperationType.Grayscale, ops[0].Type);
    }

    [Fact]
    public void ParseOperations_BlurWithRadius_ParsesRadius()
    {
        var ops = SkiaImagePipeline.ParseOperations("apply blur 7");
        Assert.Single(ops);
        Assert.Equal(SkiaImagePipeline.OperationType.Blur, ops[0].Type);
        Assert.Equal(7f, ops[0].Param1);
    }

    [Fact]
    public void ParseOperations_ResizeWithDimensions_ParsesWidthAndHeight()
    {
        var ops = SkiaImagePipeline.ParseOperations("resize 800x600");
        Assert.Single(ops);
        Assert.Equal(SkiaImagePipeline.OperationType.Resize, ops[0].Type);
        Assert.Equal(800f, ops[0].Param1);
        Assert.Equal(600f, ops[0].Param2);
    }

    [Fact]
    public void ParseOperations_MultipleKeywords_ReturnsMultipleOps()
    {
        var ops = SkiaImagePipeline.ParseOperations("grayscale, blur 3, and sharpen");
        Assert.Equal(3, ops.Count);
        Assert.Contains(ops, o => o.Type == SkiaImagePipeline.OperationType.Grayscale);
        Assert.Contains(ops, o => o.Type == SkiaImagePipeline.OperationType.Blur);
        Assert.Contains(ops, o => o.Type == SkiaImagePipeline.OperationType.Sharpen);
    }

    [Theory]
    [InlineData("black and white")]
    [InlineData("greyscale")]
    [InlineData("grayscale")]
    public void ParseOperations_GrayscaleVariants_AllRecognized(string prompt)
    {
        var ops = SkiaImagePipeline.ParseOperations(prompt);
        Assert.Contains(ops, o => o.Type == SkiaImagePipeline.OperationType.Grayscale);
    }

    [Theory]
    [InlineData("brighten 30", SkiaImagePipeline.OperationType.Brighten, 30f)]
    [InlineData("darken 15", SkiaImagePipeline.OperationType.Darken, 15f)]
    [InlineData("contrast 50", SkiaImagePipeline.OperationType.Contrast, 50f)]
    public void ParseOperations_NumericArg_ParsesCorrectly(string prompt, SkiaImagePipeline.OperationType expectedType, float expectedParam)
    {
        var ops = SkiaImagePipeline.ParseOperations(prompt);
        Assert.Single(ops);
        Assert.Equal(expectedType, ops[0].Type);
        Assert.Equal(expectedParam, ops[0].Param1);
    }
}
