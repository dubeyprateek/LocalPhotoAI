using System.Net;
using System.Net.Sockets;
using LocalPhotoAI.Host;
using LocalPhotoAI.Shared.Middleware;
using LocalPhotoAI.Shared.Models;
using LocalPhotoAI.Shared.Pipelines;
using LocalPhotoAI.Shared.Queue;
using LocalPhotoAI.Shared.Security;
using LocalPhotoAI.Shared.Storage;
using QRCoder;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var storagePath = builder.Configuration.GetValue<string>("StoragePath")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
var maxUploadSizeMb = builder.Configuration.GetValue<long>("MaxUploadSizeMB", 50);

// Shared singletons — all in-process, no cross-service coordination needed
builder.Services.AddSingleton<IPhotoStore>(new JsonPhotoStore(storagePath));
builder.Services.AddSingleton<IJobStore>(new JsonJobStore(storagePath));
builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();
builder.Services.AddSingleton<IImagePipeline, StubPipeline>();

// Background worker for processing jobs
builder.Services.AddHostedService<PhotoProcessingWorker>();

// Browser launcher (opens UI on startup, skipped during testing)
builder.Services.AddHostedService<BrowserLauncherService>();

builder.Services.AddHealthChecks();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadSizeMb * 1024 * 1024;
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

// ?? Health ???????????????????????????????????????????????????????????????????
app.MapHealthChecks("/health");

// ?? Upload endpoints (EPIC 2) ????????????????????????????????????????????????
app.MapPost("/api/uploads", async (HttpRequest request, IPhotoStore photoStore, IJobStore jobStore, IJobQueue jobQueue) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { detail = "Expected multipart/form-data." });

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files.Count == 0)
        return Results.BadRequest(new { detail = "No files provided." });

    var response = new UploadResponse();

    foreach (var file in files)
    {
        if (!FileValidation.IsAllowedExtension(file.FileName))
        {
            return Results.BadRequest(new
            {
                detail = $"File '{file.FileName}' has a disallowed extension. Allowed: {string.Join(", ", FileValidation.GetAllowedExtensions())}"
            });
        }

        var photoId = Guid.NewGuid().ToString("N");
        var sanitizedName = FileValidation.SanitizeFileName(file.FileName);
        var extension = Path.GetExtension(sanitizedName);
        var photoDir = Path.Combine(storagePath, "originals", photoId);
        Directory.CreateDirectory(photoDir);

        var filePath = Path.Combine(photoDir, $"original{extension}");
        await using (var stream = File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        var metadata = new PhotoMetadata
        {
            PhotoId = photoId,
            OriginalFileName = sanitizedName,
            OriginalPath = filePath,
            Extension = extension,
            UploadedAt = DateTime.UtcNow
        };
        await photoStore.SaveAsync(metadata);

        var jobId = Guid.NewGuid().ToString("N");
        var job = new JobRecord
        {
            JobId = jobId,
            PhotoId = photoId,
            Pipeline = "default",
            Status = JobStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        await jobStore.SaveAsync(job);

        await jobQueue.EnqueueAsync(new QueueMessage
        {
            JobId = jobId,
            PhotoId = photoId,
            Pipeline = "default"
        });

        response.Files.Add(new UploadedFileInfo
        {
            PhotoId = photoId,
            OriginalFileName = sanitizedName,
            JobId = jobId
        });
    }

    return Results.Ok(response);
});

// ?? Job endpoints (EPIC 4) ??????????????????????????????????????????????????
app.MapGet("/api/jobs", async (IJobStore store) =>
{
    var jobs = await store.GetAllAsync();
    return Results.Ok(jobs);
});

app.MapGet("/api/jobs/{jobId}", async (string jobId, IJobStore store) =>
{
    var job = await store.GetAsync(jobId);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

// ?? Gallery endpoints (EPIC 6) ??????????????????????????????????????????????
app.MapGet("/api/photos", async (IPhotoStore store) =>
{
    var photos = await store.GetAllAsync();
    return Results.Ok(photos);
});

app.MapGet("/api/photos/{photoId}", async (string photoId, IPhotoStore store) =>
{
    var photo = await store.GetAsync(photoId);
    return photo is null ? Results.NotFound() : Results.Ok(photo);
});

app.MapGet("/api/photos/{photoId}/download", async (string photoId, IPhotoStore store) =>
{
    var photo = await store.GetAsync(photoId);
    if (photo is null)
        return Results.NotFound();

    var version = photo.Versions.LastOrDefault();
    var filePath = version?.FilePath ?? photo.OriginalPath;

    if (!File.Exists(filePath))
        return Results.NotFound(new { detail = "File not found on disk." });

    var fileName = version is not null
        ? $"{Path.GetFileNameWithoutExtension(photo.OriginalFileName)}_{version.VersionName}{Path.GetExtension(filePath)}"
        : photo.OriginalFileName;

    return Results.File(filePath, "application/octet-stream", fileName);
});

// ?? Connection info & QR (EPIC 8) ??????????????????????????????????????????
app.MapGet("/api/connection-info", (HttpRequest request) =>
{
    var lanIp = NetworkHelper.GetLanIpAddress();
    var port = request.Host.Port ?? 80;
    var gatewayUrl = lanIp is not null ? $"http://{lanIp}:{port}" : null;

    return Results.Ok(new
    {
        lanIp,
        port,
        gatewayUrl,
        hostname = Dns.GetHostName(),
        localUrl = $"http://{Dns.GetHostName().ToLowerInvariant()}.local:{port}",
        note = "The .local URL requires mDNS support on your network. Use the IP-based URL as a reliable fallback."
    });
});

app.MapGet("/api/qr", (HttpRequest request) =>
{
    var lanIp = NetworkHelper.GetLanIpAddress();
    var port = request.Host.Port ?? 80;
    var url = lanIp is not null ? $"http://{lanIp}:{port}" : $"http://localhost:{port}";

    using var qrGenerator = new QRCodeGenerator();
    using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
    using var svgQrCode = new SvgQRCode(qrCodeData);
    var svgContent = svgQrCode.GetGraphic(10);

    return Results.Content(svgContent, "image/svg+xml");
});

app.Run();

// Expose the auto-generated Program class for WebApplicationFactory in tests
public partial class Program { }
