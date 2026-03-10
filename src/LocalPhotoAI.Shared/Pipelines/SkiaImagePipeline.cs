using SkiaSharp;

namespace LocalPhotoAI.Shared.Pipelines;

/// <summary>
/// Image processing pipeline powered by SkiaSharp.
/// Parses the prompt for supported transformation keywords and applies them
/// in sequence. Unrecognized prompts apply a subtle enhance (brighten + contrast + sharpen).
///
/// Supported prompt keywords:
///   grayscale / greyscale / black and white / b&amp;w / monochrome
///   sepia / vintage / retro / old photo / aged
///   blur (optionally "blur 5" for radius) / soft / dreamy / glow
///   sharpen / crisp / detail
///   brighten / brightness (optionally "brighten 20" for percent)
///   darken (optionally "darken 20" for percent)
///   contrast (optionally "contrast 30" for percent) / dramatic / cinematic
///   invert / negative
///   warm / warmer / sunset / golden
///   cool / cooler / cold / blue tone
///   saturate / vivid / pop / vibrant / colorful
///   enhance / improve / auto / fix / optimize
///   resize WxH (e.g., "resize 800x600")
///   rotate N (e.g., "rotate 90")
///   flip horizontal / flip vertical / mirror
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

    public static List<ImageOperation> ParseOperations(string? prompt)
    {
        var ops = new List<ImageOperation>();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ops.Add(new ImageOperation(OperationType.Grayscale));
            return ops;
        }

        var lower = prompt.ToLowerInvariant();

        // -- Grayscale variants --
        if (lower.Contains("grayscale") || lower.Contains("greyscale") || lower.Contains("black and white")
            || lower.Contains("b&w") || lower.Contains("monochrome"))
            ops.Add(new ImageOperation(OperationType.Grayscale));

        // -- Sepia / vintage variants --
        if (lower.Contains("sepia") || lower.Contains("vintage") || lower.Contains("retro")
            || lower.Contains("old photo") || lower.Contains("aged"))
            ops.Add(new ImageOperation(OperationType.Sepia));

        // -- Invert --
        if (lower.Contains("invert") || lower.Contains("negative"))
            ops.Add(new ImageOperation(OperationType.Invert));

        // -- Blur / soft variants --
        if (lower.Contains("blur") || lower.Contains("soft") || lower.Contains("dreamy") || lower.Contains("glow"))
        {
            var radius = ParseNumericArg(lower, "blur", 3f);
            ops.Add(new ImageOperation(OperationType.Blur, radius));
        }

        // -- Sharpen / crisp variants --
        if (lower.Contains("sharpen") || lower.Contains("crisp") || lower.Contains("detail"))
            ops.Add(new ImageOperation(OperationType.Sharpen));

        // -- Brighten --
        if (lower.Contains("brighten") || lower.Contains("brightness"))
        {
            var amount = ParseNumericArg(lower, lower.Contains("brighten") ? "brighten" : "brightness", 20f);
            ops.Add(new ImageOperation(OperationType.Brighten, amount));
        }

        // -- Darken --
        if (lower.Contains("darken"))
        {
            var amount = ParseNumericArg(lower, "darken", 20f);
            ops.Add(new ImageOperation(OperationType.Darken, amount));
        }

        // -- Contrast / dramatic variants --
        if (lower.Contains("contrast") || lower.Contains("dramatic") || lower.Contains("cinematic"))
        {
            var amount = ParseNumericArg(lower, "contrast", 30f);
            ops.Add(new ImageOperation(OperationType.Contrast, amount));
        }

        // -- Warm tone --
        if (lower.Contains("warm") || lower.Contains("sunset") || lower.Contains("golden"))
            ops.Add(new ImageOperation(OperationType.Warm));

        // -- Cool tone --
        if (lower.Contains("cool") || lower.Contains("cold") || lower.Contains("blue tone"))
            ops.Add(new ImageOperation(OperationType.Cool));

        // -- Saturate / vivid variants --
        if (lower.Contains("saturate") || lower.Contains("vivid") || lower.Contains("pop")
            || lower.Contains("vibrant") || lower.Contains("colorful"))
            ops.Add(new ImageOperation(OperationType.Saturate));

        // -- Enhance / auto-improve combo --
        if (lower.Contains("enhance") || lower.Contains("improve") || lower.Contains("auto")
            || lower.Contains("fix") || lower.Contains("optimize"))
        {
            ops.Add(new ImageOperation(OperationType.Brighten, 10f));
            ops.Add(new ImageOperation(OperationType.Contrast, 20f));
            ops.Add(new ImageOperation(OperationType.Sharpen));
        }

        // -- Resize WxH --
        var resizeMatch = System.Text.RegularExpressions.Regex.Match(lower, @"resize\s+(\d+)\s*x\s*(\d+)");
        if (resizeMatch.Success)
        {
            var w = float.Parse(resizeMatch.Groups[1].Value);
            var h = float.Parse(resizeMatch.Groups[2].Value);
            ops.Add(new ImageOperation(OperationType.Resize, w, h));
        }

        // -- Rotate --
        if (lower.Contains("rotate"))
        {
            var degrees = ParseNumericArg(lower, "rotate", 90f);
            ops.Add(new ImageOperation(OperationType.Rotate, degrees));
        }

        // -- Flip / mirror --
        if (lower.Contains("flip horizontal") || lower.Contains("mirror"))
            ops.Add(new ImageOperation(OperationType.FlipHorizontal));

        if (lower.Contains("flip vertical"))
            ops.Add(new ImageOperation(OperationType.FlipVertical));

        // If nothing was recognized, apply a subtle enhance instead of grayscale
        if (ops.Count == 0)
        {
            ops.Add(new ImageOperation(OperationType.Brighten, 10f));
            ops.Add(new ImageOperation(OperationType.Contrast, 20f));
            ops.Add(new ImageOperation(OperationType.Sharpen));
        }

        return ops;
    }

    private static float ParseNumericArg(string text, string keyword, float defaultValue)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, keyword + @"\s+([\d.]+)");
        return match.Success && float.TryParse(match.Groups[1].Value, out var val) ? val : defaultValue;
    }

    public static SKBitmap ApplyOperations(SKBitmap source, List<ImageOperation> operations)
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
                OperationType.Warm => ApplyColorFilter(current, CreateWarmMatrix()),
                OperationType.Cool => ApplyColorFilter(current, CreateCoolMatrix()),
                OperationType.Saturate => ApplyColorFilter(current, CreateSaturateMatrix()),
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

    public static float[] CreateGrayscaleMatrix() =>
    [
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0,       0,       0,       1, 0
    ];

    public static float[] CreateSepiaMatrix() =>
    [
        0.393f, 0.769f, 0.189f, 0, 0,
        0.349f, 0.686f, 0.168f, 0, 0,
        0.272f, 0.534f, 0.131f, 0, 0,
        0,      0,      0,      1, 0
    ];

    public static float[] CreateInvertMatrix() =>
    [
        -1,  0,  0, 0, 1,
         0, -1,  0, 0, 1,
         0,  0, -1, 0, 1,
         0,  0,  0, 1, 0
    ];

    public static float[] CreateBrightnessMatrix(float amount) =>
    [
        1, 0, 0, 0, amount,
        0, 1, 0, 0, amount,
        0, 0, 1, 0, amount,
        0, 0, 0, 1, 0
    ];

    public static float[] CreateContrastMatrix(float factor)
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

    public static float[] CreateWarmMatrix() =>
    [
        1.2f, 0,    0,    0, 0.05f,
        0,    1.0f, 0,    0, 0.02f,
        0,    0,    0.8f, 0, 0,
        0,    0,    0,    1, 0
    ];

    public static float[] CreateCoolMatrix() =>
    [
        0.85f, 0,    0,    0, 0,
        0,     1.0f, 0,    0, 0.02f,
        0,     0,    1.2f, 0, 0.05f,
        0,     0,    0,    1, 0
    ];

    public static float[] CreateSaturateMatrix() =>
    [
        1.3f,  -0.15f, -0.15f, 0, 0,
        -0.15f, 1.3f,  -0.15f, 0, 0,
        -0.15f, -0.15f, 1.3f,  0, 0,
        0,      0,      0,     1, 0
    ];

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
        Warm,
        Cool,
        Saturate,
        Resize,
        Rotate,
        FlipHorizontal,
        FlipVertical
    }

    public record ImageOperation(OperationType Type, float Param1 = 0f, float Param2 = 0f);
}
