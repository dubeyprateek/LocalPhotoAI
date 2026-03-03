using LocalPhotoAI.Shared.Models;
using LocalPhotoAI.Shared.Storage;

namespace LocalPhotoAI.Tests;

public class JsonStoreTests : IDisposable
{
    private readonly string _tempDir;

    public JsonStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "store_test_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task PhotoStore_SaveAndRetrieve()
    {
        var store = new JsonPhotoStore(_tempDir);
        var photo = new PhotoMetadata
        {
            PhotoId = "test1",
            OriginalFileName = "photo.jpg",
            OriginalPath = "/storage/originals/test1/original.jpg",
            Extension = ".jpg"
        };

        await store.SaveAsync(photo);
        var retrieved = await store.GetAsync("test1");

        Assert.NotNull(retrieved);
        Assert.Equal("test1", retrieved.PhotoId);
        Assert.Equal("photo.jpg", retrieved.OriginalFileName);
    }

    [Fact]
    public async Task PhotoStore_GetAll_ReturnsAllPhotos()
    {
        var store = new JsonPhotoStore(_tempDir);
        await store.SaveAsync(new PhotoMetadata { PhotoId = "p1", OriginalFileName = "a.jpg" });
        await store.SaveAsync(new PhotoMetadata { PhotoId = "p2", OriginalFileName = "b.jpg" });

        var all = await store.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task PhotoStore_PersistsAcrossInstances()
    {
        var store1 = new JsonPhotoStore(_tempDir);
        await store1.SaveAsync(new PhotoMetadata { PhotoId = "persist1", OriginalFileName = "test.png" });

        // Create a new instance pointing to the same directory
        var store2 = new JsonPhotoStore(_tempDir);
        var retrieved = await store2.GetAsync("persist1");

        Assert.NotNull(retrieved);
        Assert.Equal("persist1", retrieved.PhotoId);
    }

    [Fact]
    public async Task PhotoStore_GetNonExistent_ReturnsNull()
    {
        var store = new JsonPhotoStore(_tempDir);
        var result = await store.GetAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task JobStore_SaveAndRetrieve()
    {
        var store = new JsonJobStore(_tempDir);
        var job = new JobRecord
        {
            JobId = "job1",
            PhotoId = "photo1",
            Pipeline = "default",
            Status = JobStatus.Queued
        };

        await store.SaveAsync(job);
        var retrieved = await store.GetAsync("job1");

        Assert.NotNull(retrieved);
        Assert.Equal("job1", retrieved.JobId);
        Assert.Equal(JobStatus.Queued, retrieved.Status);
    }

    [Fact]
    public async Task JobStore_Update_ChangesStatus()
    {
        var store = new JsonJobStore(_tempDir);
        var job = new JobRecord { JobId = "job2", PhotoId = "p2", Status = JobStatus.Queued };
        await store.SaveAsync(job);

        job.Status = JobStatus.Processing;
        await store.UpdateAsync(job);

        var retrieved = await store.GetAsync("job2");
        Assert.NotNull(retrieved);
        Assert.Equal(JobStatus.Processing, retrieved.Status);
    }

    [Fact]
    public async Task JobStore_PersistsAcrossInstances()
    {
        var store1 = new JsonJobStore(_tempDir);
        await store1.SaveAsync(new JobRecord { JobId = "j-persist", PhotoId = "p1", Status = JobStatus.Succeeded });

        var store2 = new JsonJobStore(_tempDir);
        var retrieved = await store2.GetAsync("j-persist");

        Assert.NotNull(retrieved);
        Assert.Equal(JobStatus.Succeeded, retrieved.Status);
    }
}
