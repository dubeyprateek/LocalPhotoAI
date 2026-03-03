using LocalPhotoAI.Shared.Models;
using LocalPhotoAI.Shared.Pipelines;
using LocalPhotoAI.Shared.Queue;
using LocalPhotoAI.Shared.Storage;

namespace WorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IJobQueue _queue;
    private readonly IJobStore _jobStore;
    private readonly IPhotoStore _photoStore;
    private readonly IImagePipeline _pipeline;
    private readonly string _storagePath;

    public Worker(
        ILogger<Worker> logger,
        IJobQueue queue,
        IJobStore jobStore,
        IPhotoStore photoStore,
        IImagePipeline pipeline,
        IConfiguration configuration)
    {
        _logger = logger;
        _queue = queue;
        _jobStore = jobStore;
        _photoStore = photoStore;
        _pipeline = pipeline;
        _storagePath = configuration.GetValue<string>("StoragePath") ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started, waiting for jobs...");

        while (!stoppingToken.IsCancellationRequested)
        {
            QueueMessage message;
            try
            {
                message = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _logger.LogInformation("Processing job {JobId} for photo {PhotoId}", message.JobId, message.PhotoId);

            var job = await _jobStore.GetAsync(message.JobId);
            if (job is null)
            {
                _logger.LogWarning("Job {JobId} not found, skipping", message.JobId);
                continue;
            }

            job.Status = JobStatus.Processing;
            await _jobStore.UpdateAsync(job);

            var photo = await _photoStore.GetAsync(message.PhotoId);
            if (photo is null)
            {
                job.Status = JobStatus.Failed;
                job.Error = "Photo metadata not found.";
                job.CompletedAt = DateTime.UtcNow;
                await _jobStore.UpdateAsync(job);
                continue;
            }

            try
            {
                var outputDir = Path.Combine(_storagePath, "edited", photo.PhotoId);
                var result = await _pipeline.RunAsync(photo.OriginalPath, outputDir, stoppingToken);

                if (result.Success)
                {
                    photo.Versions.Add(new PhotoVersion
                    {
                        VersionName = _pipeline.Name,
                        FilePath = result.OutputPath,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _photoStore.UpdateAsync(photo);

                    job.Status = JobStatus.Succeeded;
                    job.Progress = 100;
                    _logger.LogInformation("Job {JobId} succeeded, output at {Path}", job.JobId, result.OutputPath);
                }
                else
                {
                    job.Status = JobStatus.Failed;
                    job.Error = result.Error ?? "Pipeline returned failure.";
                    _logger.LogWarning("Job {JobId} failed: {Error}", job.JobId, job.Error);
                }
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.Error = ex.Message;
                _logger.LogError(ex, "Job {JobId} threw an exception", job.JobId);
            }

            job.CompletedAt = DateTime.UtcNow;
            await _jobStore.UpdateAsync(job);
        }
    }
}
