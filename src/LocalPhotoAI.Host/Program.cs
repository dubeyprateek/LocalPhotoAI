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
var maxUploadSizeMb = builder.Configuration.GetValue<long>("MaxUploadSizeMB", 200);

// Shared singletons -- all in-process, no cross-service coordination needed
builder.Services.AddSingleton<IPhotoStore>(new JsonPhotoStore(storagePath));
builder.Services.AddSingleton<IJobStore>(new JsonJobStore(storagePath));
builder.Services.AddSingleton<ISessionStore>(new JsonSessionStore(storagePath));
builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();

// Image pipeline: use SkiaSharp-based pipeline for real processing,
// fall back to stub when Pipeline config is set to "stub"
var pipelineMode = builder.Configuration.GetValue<string>("Pipeline") ?? "skia";
if (pipelineMode.Equals("stub", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IImagePipeline, StubPipeline>();
else
    builder.Services.AddSingleton<IImagePipeline, SkiaImagePipeline>();

// Prompt refiner: use OpenAI when API key is configured, otherwise stub
var openAiKey = builder.Configuration.GetValue<string>("OpenAI:ApiKey");
var openAiModel = builder.Configuration.GetValue<string>("OpenAI:Model") ?? "gpt-4o-mini";
if (!string.IsNullOrWhiteSpace(openAiKey))
{
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<IPromptRefiner>(sp =>
        new OpenAIPromptRefiner(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), openAiKey, openAiModel));
}
else
{
    builder.Services.AddSingleton<IPromptRefiner, StubPromptRefiner>();
}

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
    // Disable request size limit for file uploads
    var sizeFeature = request.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (sizeFeature is not null) sizeFeature.MaxRequestBodySize = null;

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

    var fullPath = Path.GetFullPath(filePath);
    if (!File.Exists(fullPath))
        return Results.NotFound(new { detail = "File not found on disk." });

    var fileName = version is not null
        ? $"{Path.GetFileNameWithoutExtension(photo.OriginalFileName)}_{version.VersionName}{Path.GetExtension(fullPath)}"
        : photo.OriginalFileName;

    var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    return Results.File(stream, "application/octet-stream", fileName);
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

// ?? Prompt refinement endpoint ??????????????????????????????????????????????
app.MapPost("/api/prompt/refine", async (HttpRequest request, IPromptRefiner refiner) =>
{
    var body = await request.ReadFromJsonAsync<PromptRefineRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        return Results.BadRequest(new { detail = "Prompt is required." });

    var result = await refiner.RefineAsync(body.Prompt);
    return Results.Ok(new { refinedPrompt = result.RefinedPrompt, title = result.GeneratedTitle });
});

// ??? Session endpoints ???????????????????????????????????????????????????????
app.MapPost("/api/sessions", async (HttpRequest request, ISessionStore sessionStore, IPromptRefiner refiner) =>
{
    var body = await request.ReadFromJsonAsync<CreateSessionRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        return Results.BadRequest(new { detail = "Prompt is required." });

    var refined = await refiner.RefineAsync(body.Prompt);
    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var folderName = $"{refined.GeneratedTitle}_{timestamp}";
    var inputFolder = Path.Combine(storagePath, "sessions", folderName, "InputImages");
    var outputFolder = Path.Combine(storagePath, "sessions", folderName, "OutputImages");
    Directory.CreateDirectory(inputFolder);
    Directory.CreateDirectory(outputFolder);

    var session = new SessionRecord
    {
        SessionId = Guid.NewGuid().ToString("N"),
        UserPrompt = body.Prompt,
        RefinedPrompt = refined.RefinedPrompt,
        Title = refined.GeneratedTitle,
        FolderName = folderName,
        InputFolder = inputFolder,
        OutputFolder = outputFolder,
        Status = SessionStatus.Draft
    };
    await sessionStore.SaveAsync(session);

    return Results.Ok(session);
});

app.MapPost("/api/sessions/{sessionId}/refine", async (string sessionId, HttpRequest request, ISessionStore sessionStore, IPromptRefiner refiner) =>
{
    var session = await sessionStore.GetAsync(sessionId);
    if (session is null)
        return Results.NotFound();

    var body = await request.ReadFromJsonAsync<PromptRefineRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        return Results.BadRequest(new { detail = "Prompt is required." });

    var refined = await refiner.RefineAsync(body.Prompt);
    session.UserPrompt = body.Prompt;
    session.RefinedPrompt = refined.RefinedPrompt;
    session.Title = refined.GeneratedTitle;
    await sessionStore.UpdateAsync(session);

    return Results.Ok(new { refinedPrompt = refined.RefinedPrompt, title = refined.GeneratedTitle });
});

app.MapGet("/api/sessions", async (ISessionStore store) =>
{
    var sessions = await store.GetAllAsync();
    return Results.Ok(sessions);
});

app.MapGet("/api/sessions/{sessionId}", async (string sessionId, ISessionStore store) =>
{
    var session = await store.GetAsync(sessionId);
    return session is null ? Results.NotFound() : Results.Ok(session);
});

// ?? Session upload — saves files to session InputImages folder and queues jobs
app.MapPost("/api/sessions/{sessionId}/upload", async (string sessionId, HttpRequest request,
    ISessionStore sessionStore, IPhotoStore photoStore, IJobStore jobStore, IJobQueue jobQueue) =>
{
    var session = await sessionStore.GetAsync(sessionId);
    if (session is null)
        return Results.NotFound(new { detail = "Session not found." });

    // Disable request size limit for file uploads
    var sizeFeature = request.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (sizeFeature is not null) sizeFeature.MaxRequestBodySize = null;

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

        // Save to session's InputImages folder
        var filePath = Path.Combine(session.InputFolder, $"{photoId}{extension}");
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
            SessionId = sessionId,
            Prompt = session.RefinedPrompt,
            Status = JobStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        await jobStore.SaveAsync(job);

        await jobQueue.EnqueueAsync(new QueueMessage
        {
            JobId = jobId,
            PhotoId = photoId,
            Pipeline = "default",
            SessionId = sessionId,
            Prompt = session.RefinedPrompt
        });

        session.PhotoIds.Add(photoId);
        session.JobIds.Add(jobId);

        response.Files.Add(new UploadedFileInfo
        {
            PhotoId = photoId,
            OriginalFileName = sanitizedName,
            JobId = jobId
        });
    }

    session.Status = SessionStatus.Processing;
    await sessionStore.UpdateAsync(session);

    return Results.Ok(response);
});

// ?? Session results — returns output images for a session
app.MapGet("/api/sessions/{sessionId}/results", async (string sessionId,
    ISessionStore sessionStore, IPhotoStore photoStore) =>
{
    var session = await sessionStore.GetAsync(sessionId);
    if (session is null)
        return Results.NotFound();

    var results = new List<object>();
    foreach (var photoId in session.PhotoIds)
    {
        var photo = await photoStore.GetAsync(photoId);
        if (photo is null) continue;

        var latestVersion = photo.Versions.LastOrDefault();
        results.Add(new
        {
            photoId = photo.PhotoId,
            originalFileName = photo.OriginalFileName,
            hasOutput = latestVersion is not null,
            downloadUrl = $"/api/photos/{photo.PhotoId}/download"
        });
    }

    return Results.Ok(new
    {
        session.SessionId,
        session.Title,
        session.Status,
        session.RefinedPrompt,
        photos = results
    });
});

// Generate QR code for a specific session results page
app.MapGet("/api/sessions/{sessionId}/qr", async (string sessionId, HttpRequest request, ISessionStore sessionStore) =>
{
    var session = await sessionStore.GetAsync(sessionId);
    if (session is null)
        return Results.NotFound();

    var lanIp = NetworkHelper.GetLanIpAddress();
    var port = request.Host.Port ?? 80;
    var baseUrl = lanIp is not null ? $"http://{lanIp}:{port}" : $"http://localhost:{port}";
    var url = $"{baseUrl}?session={sessionId}";

    using var qrGenerator = new QRCodeGenerator();
    using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
    using var svgQrCode = new SvgQRCode(qrCodeData);
    var svgContent = svgQrCode.GetGraphic(10);

    return Results.Content(svgContent, "image/svg+xml");
});

// Delete a session and its associated files, photos, and jobs
app.MapDelete("/api/sessions/{sessionId}", async (string sessionId,
    ISessionStore sessionStore, IPhotoStore photoStore, IJobStore jobStore, ILogger<Program> logger) =>
{
    var session = await sessionStore.GetAsync(sessionId);
    if (session is null)
        return Results.NotFound();

    // Delete associated photo files and metadata
    foreach (var photoId in session.PhotoIds)
    {
        var photo = await photoStore.GetAsync(photoId);
        if (photo is not null)
        {
            // Delete original file
            try { if (File.Exists(photo.OriginalPath)) File.Delete(photo.OriginalPath); } catch { /* best effort */ }

            // Delete version files
            foreach (var version in photo.Versions)
            {
                try { if (File.Exists(version.FilePath)) File.Delete(version.FilePath); } catch { /* best effort */ }
            }

            await photoStore.DeleteAsync(photoId);
        }
    }

    // Delete associated job metadata
    foreach (var jobId in session.JobIds)
    {
        await jobStore.DeleteAsync(jobId);
    }

    // Delete session folders from disk
    if (!string.IsNullOrEmpty(session.InputFolder))
    {
        try { if (Directory.Exists(session.InputFolder)) Directory.Delete(session.InputFolder, recursive: true); } catch { /* best effort */ }
    }
    if (!string.IsNullOrEmpty(session.OutputFolder))
    {
        try { if (Directory.Exists(session.OutputFolder)) Directory.Delete(session.OutputFolder, recursive: true); } catch { /* best effort */ }
    }
    // Try to delete the parent session folder if empty
    if (!string.IsNullOrEmpty(session.FolderName))
    {
        var sessionDir = Path.Combine(storagePath, "sessions", session.FolderName);
        try { if (Directory.Exists(sessionDir) && !Directory.EnumerateFileSystemEntries(sessionDir).Any()) Directory.Delete(sessionDir); } catch { /* best effort */ }
    }

    await sessionStore.DeleteAsync(sessionId);
    logger.LogInformation("Deleted session {SessionId} and its files", sessionId);

    return Results.NoContent();
});

app.Run();

// Expose the auto-generated Program class for WebApplicationFactory in tests
public partial class Program { }

// Request DTOs
public record PromptRefineRequest(string Prompt);
public record CreateSessionRequest(string Prompt);
