using LocalPhotoAI.Shared.Middleware;
using LocalPhotoAI.Shared.Models;
using LocalPhotoAI.Shared.Queue;
using LocalPhotoAI.Shared.Security;
using LocalPhotoAI.Shared.Storage;

var builder = WebApplication.CreateBuilder(args);

var storagePath = builder.Configuration.GetValue<string>("StoragePath") ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
var maxUploadSizeMb = builder.Configuration.GetValue<long>("MaxUploadSizeMB", 50);

builder.Services.AddSingleton<IPhotoStore>(new JsonPhotoStore(storagePath));
builder.Services.AddSingleton<IJobStore>(new JsonJobStore(storagePath));
builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();
builder.Services.AddHealthChecks();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadSizeMb * 1024 * 1024;
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/health");

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

app.Run();

// Expose the auto-generated Program class for WebApplicationFactory in tests
public partial class Program { }
