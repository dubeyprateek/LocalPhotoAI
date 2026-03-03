using LocalPhotoAI.Shared.Middleware;
using LocalPhotoAI.Shared.Storage;

var builder = WebApplication.CreateBuilder(args);

var storagePath = builder.Configuration.GetValue<string>("StoragePath") ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");

builder.Services.AddSingleton<IJobStore>(new JsonJobStore(storagePath));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/health");

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

app.Run();
