using LocalPhotoAI.Shared.Pipelines;
using LocalPhotoAI.Shared.Queue;
using LocalPhotoAI.Shared.Storage;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);

var storagePath = builder.Configuration.GetValue<string>("StoragePath") ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");

builder.Services.AddSingleton<IPhotoStore>(new JsonPhotoStore(storagePath));
builder.Services.AddSingleton<IJobStore>(new JsonJobStore(storagePath));
builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();
builder.Services.AddSingleton<IImagePipeline, StubPipeline>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
