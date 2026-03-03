using LocalPhotoAI.Shared.Middleware;
using LocalPhotoAI.Shared.Storage;

var builder = WebApplication.CreateBuilder(args);

var storagePath = builder.Configuration.GetValue<string>("StoragePath") ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");

builder.Services.AddSingleton<IPhotoStore>(new JsonPhotoStore(storagePath));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/health");

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

    // Prefer edited version, fall back to original
    var version = photo.Versions.LastOrDefault();
    var filePath = version?.FilePath ?? photo.OriginalPath;

    if (!File.Exists(filePath))
        return Results.NotFound(new { detail = "File not found on disk." });

    var fileName = version is not null
        ? $"{Path.GetFileNameWithoutExtension(photo.OriginalFileName)}_{version.VersionName}{Path.GetExtension(filePath)}"
        : photo.OriginalFileName;

    return Results.File(filePath, "application/octet-stream", fileName);
});

app.Run();
