using SkiaSharp;

namespace LocalPhotoAI.Shared.Pipelines;

/// <summary>
/// Image processing pipeline powered by SkiaSharp.
/// Parses the prompt for supported transformation keywords and applies them
/// in sequence. Falls back to a copy if no recognized operations are found.
///
/// Supported prompt keywords:
///   grayscale / greyscale / black and white
///   sepia
///   blur (optionally "blur 5" for radius)
///   sharpen
///   brighten / brightness (optionally "brighten 20" for percent)
///   darken (optionally "darken 20" for percent)
///   contrast (optionally "contrast 30" for percent)
///   invert / negative
///   resize WxH (e.g., "resize 800x600")
///   rotate N (e.g., "rotate 90")
///   flip horizontal / flip vertical
/// </summary>
public class SkiaImagePipeline : IImagePipeline
{
    public string Name => "skia";

    public Task<PipelineResult> RunAsync(string inputPath, string outputDir, CancellationToken cancellationToken = default)
    {
        return RunAsync(inputPath, outputDir, prompt: null, cancellationToken);
    }

    public Task<PipelineResult> RunAsync(string inputPath, string outputDir, string? prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

            using var inputStream = File.OpenRead(inputPath);
            using var original = SKBitmap.Decode(inputStream);

            if (original is null)
            {
                return Task.FromResult(new PipelineResult
                {
                    Success = false,
                    Error = "Failed to decode image. The file may be corrupt or in an unsupported format."
                });
            }

            var operations = ParseOperations(prompt);
            var processed = ApplyOperations(original, operations);

            var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + ".png";
            var outputPath = Path.Combine(outputDir, outputFileName);

            using var image = SKImage.FromBitmap(processed);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var outputStream = File.Create(outputPath);
            data.SaveTo(outputStream);

            if (!ReferenceEquals(processed, original))
                processed.Dispose();

            return Task.FromResult(new PipelineResult
            {
                Success = true,
                OutputPath = outputPath
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new PipelineResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    internal static List<ImageOperation> ParseOperations(string? prompt)
    {
        var ops = new List<ImageOperation>();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ops.Add(new ImageOperation(OperationType.Grayscale));
            return ops;
        }

        var lower = prompt.ToLowerInvariant();

        if (lower.Contains("grayscale") || lower.Contains("greyscale") || lower.Contains("black and white"))
            ops.Add(new ImageOperation(OperationType.Grayscale));

        if (lower.Contains("sepia"))
            ops.Add(new ImageOperation(OperationType.Sepia));

        if (lower.Contains("invert") || lower.Contains("negative"))
            ops.Add(new ImageOperation(OperationType.Invert));

        // blur with optional radius: "blur 5" or "blur"
        if (lower.Contains("blur"))
        {
            var radius = ParseNumericArg(lower, "blur", 3f);
            ops.Add(new ImageOperation(OperationType.Blur, radius));
        }

        if (lower.Contains("sharpen"))
            ops.Add(new ImageOperation(OperationType.Sharpen));

        if (lower.Contains("brighten") || lower.Contains("brightness"))
        {
            var amount = ParseNumericArg(lower, lower.Contains("brighten") ? "brighten" : "brightness", 20f);
            ops.Add(new ImageOperation(OperationType.Brighten, amount));
        }

        if (lower.Contains("darken"))
        {
            var amount = ParseNumericArg(lower, "darken", 20f);
            ops.Add(new ImageOperation(OperationType.Darken, amount));
        }

        if (lower.Contains("contrast"))
        {
            var amount = ParseNumericArg(lower, "contrast", 30f);
            ops.Add(new ImageOperation(OperationType.Contrast, amount));
        }

        // resize WxH: "resize 800x600"
        var resizeMatch = System.Text.RegularExpressions.Regex.Match(lower, @"resize\s+(\d+)\s*x\s*(\d+)");
        if (resizeMatch.Success)
        {
            var w = float.Parse(resizeMatch.Groups[1].Value);
            var h = float.Parse(resizeMatch.Groups[2].Value);
            ops.Add(new ImageOperation(OperationType.Resize, w, h));
        }

        // rotate N: "rotate 90"
        if (lower.Contains("rotate"))
        {
            var degrees = ParseNumericArg(lower, "rotate", 90f);
            ops.Add(new ImageOperation(OperationType.Rotate, degrees));
        }

        if (lower.Contains("flip horizontal"))
            ops.Add(new ImageOperation(OperationType.FlipHorizontal));

        if (lower.Contains("flip vertical"))
            ops.Add(new ImageOperation(OperationType.FlipVertical));

        // If nothing was recognized, default to grayscale
        if (ops.Count == 0)
            ops.Add(new ImageOperation(OperationType.Grayscale));

        return ops;
    }

    private static float ParseNumericArg(string text, string keyword, float defaultValue)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, keyword + @"\s+([\d.]+)");
        return match.Success && float.TryParse(match.Groups[1].Value, out var val) ? val : defaultValue;
    }

    internal static SKBitmap ApplyOperations(SKBitmap source, List<ImageOperation> operations)
    {
        var current = source;

        foreach (var op in operations)
        {
            var next = op.Type switch
            {
                OperationType.Grayscale => ApplyColorFilter(current, CreateGrayscaleMatrix()),
                OperationType.Sepia => ApplyColorFilter(current, CreateSepiaMatrix()),
                OperationType.Invert => ApplyColorFilter(current, CreateInvertMatrix()),
                OperationType.Blur => ApplyBlur(current, op.Param1),
                OperationType.Sharpen => ApplySharpen(current),
                OperationType.Brighten => ApplyColorFilter(current, CreateBrightnessMatrix(op.Param1 / 100f)),
                OperationType.Darken => ApplyColorFilter(current, CreateBrightnessMatrix(-op.Param1 / 100f)),
                OperationType.Contrast => ApplyColorFilter(current, CreateContrastMatrix(1f + op.Param1 / 100f)),
                OperationType.Resize => ApplyResize(current, (int)op.Param1, (int)op.Param2),
                OperationType.Rotate => ApplyRotate(current, op.Param1),
                OperationType.FlipHorizontal => ApplyFlip(current, horizontal: true),
                OperationType.FlipVertical => ApplyFlip(current, horizontal: false),
                _ => current
            };

            if (!ReferenceEquals(next, current) && !ReferenceEquals(current, source))
                current.Dispose();

            current = next;
        }

        return current;
    }

    // -- Color matrix filters ------------------------------------------------

    private static SKBitmap ApplyColorFilter(SKBitmap source, float[] matrix)
    {
        var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint();
        paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0, paint);
        return result;
    }

    internal static float[] CreateGrayscaleMatrix() =>
    [
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0,       0,       0,       1, 0
    ];

    internal static float[] CreateSepiaMatrix() =>
    [
        0.393f, 0.769f, 0.189f, 0, 0,
        0.349f, 0.686f, 0.168f, 0, 0,
        0.272f, 0.534f, 0.131f, 0, 0,
        0,      0,      0,      1, 0
    ];

    internal static float[] CreateInvertMatrix() =>
    [
        -1,  0,  0, 0, 1,
         0, -1,  0, 0, 1,
         0,  0, -1, 0, 1,
         0,  0,  0, 1, 0
    ];

    internal static float[] CreateBrightnessMatrix(float amount) =>
    [
        1, 0, 0, 0, amount,
        0, 1, 0, 0, amount,
        0, 0, 1, 0, amount,
        0, 0, 0, 1, 0
    ];

    internal static float[] CreateContrastMatrix(float factor)
    {
        var t = (1f - factor) / 2f;
        return
        [
            factor, 0,      0,      0, t,
            0,      factor, 0,      0, t,
            0,      0,      factor, 0, t,
            0,      0,      0,      1, 0
        ];
    }

    // -- Spatial filters -----------------------------------------------------

    private static SKBitmap ApplyBlur(SKBitmap source, float radius)
    {
        var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint();
        paint.ImageFilter = SKImageFilter.CreateBlur(radius, radius);
        canvas.DrawBitmap(source, 0, 0, paint);
        return result;
    }

    private static SKBitmap ApplySharpen(SKBitmap source)
    {
        // 3x3 sharpening convolution kernel
        var kernel = new float[]
        {
             0, -1,  0,
            -1,  5, -1,
             0, -1,  0
        };

        var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint();
        paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
            new SKSizeI(3, 3), kernel, gain: 1f, bias: 0f,
            new SKPointI(1, 1), SKShaderTileMode.Clamp, convolveAlpha: false);
        canvas.DrawBitmap(source, 0, 0, paint);
        return result;
    }

    // -- Geometric transforms ------------------------------------------------

    private static SKBitmap ApplyResize(SKBitmap source, int width, int height)
    {
        var result = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint();
        canvas.DrawBitmap(source, SKRect.Create(width, height), paint);
        return result;
    }

    private static SKBitmap ApplyRotate(SKBitmap source, float degrees)
    {
        // For 90/180/270 keep tight bounds; otherwise use rotated bounds
        var radians = degrees * (float)Math.PI / 180f;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        var newWidth = (int)(source.Width * cos + source.Height * sin);
        var newHeight = (int)(source.Width * sin + source.Height * cos);

        var result = new SKBitmap(newWidth, newHeight, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(newWidth / 2f, newHeight / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static SKBitmap ApplyFlip(SKBitmap source, bool horizontal)
    {
        var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        if (horizontal)
        {
            canvas.Scale(-1, 1, source.Width / 2f, 0);
        }
        else
        {
            canvas.Scale(1, -1, 0, source.Height / 2f);
        }
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    // -- Types ---------------------------------------------------------------

    public enum OperationType
    {
        Grayscale,
        Sepia,
        Invert,
        Blur,
        Sharpen,
        Brighten,
        Darken,
        Contrast,
        Resize,
        Rotate,
        FlipHorizontal,
        FlipVertical
    }

    public record ImageOperation(OperationType Type, float Param1 = 0f, float Param2 = 0f);
}
