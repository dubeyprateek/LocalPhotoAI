using LocalPhotoAI.Shared.Models;
using LocalPhotoAI.Shared.Storage;

namespace LocalPhotoAI.Tests;

public class SessionStoreTests : IDisposable
{
    private readonly string _tempDir;

    public SessionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "session_store_test_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAndRetrieve_RoundTrips()
    {
        var store = new JsonSessionStore(_tempDir);
        var session = new SessionRecord
        {
            SessionId = "s1",
            UserPrompt = "remove background",
            RefinedPrompt = "Apply the following transformation...",
            Title = "remove-background",
            FolderName = "remove-background_20250101-120000",
            InputFolder = "/input",
            OutputFolder = "/output",
            Status = SessionStatus.Draft
        };

        await store.SaveAsync(session);
        var retrieved = await store.GetAsync("s1");

        Assert.NotNull(retrieved);
        Assert.Equal("s1", retrieved.SessionId);
        Assert.Equal("remove background", retrieved.UserPrompt);
        Assert.Equal("remove-background", retrieved.Title);
        Assert.Equal(SessionStatus.Draft, retrieved.Status);
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var store = new JsonSessionStore(_tempDir);
        var result = await store.GetAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSessions()
    {
        var store = new JsonSessionStore(_tempDir);
        await store.SaveAsync(new SessionRecord { SessionId = "s1", Title = "a" });
        await store.SaveAsync(new SessionRecord { SessionId = "s2", Title = "b" });
        await store.SaveAsync(new SessionRecord { SessionId = "s3", Title = "c" });

        var all = await store.GetAllAsync();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task UpdateAsync_ChangesStatus()
    {
        var store = new JsonSessionStore(_tempDir);
        var session = new SessionRecord { SessionId = "s1", Status = SessionStatus.Draft };
        await store.SaveAsync(session);

        session.Status = SessionStatus.Processing;
        await store.UpdateAsync(session);

        var retrieved = await store.GetAsync("s1");
        Assert.NotNull(retrieved);
        Assert.Equal(SessionStatus.Processing, retrieved.Status);
    }

    [Fact]
    public async Task UpdateAsync_PersistsPhotoAndJobIds()
    {
        var store = new JsonSessionStore(_tempDir);
        var session = new SessionRecord { SessionId = "s1" };
        await store.SaveAsync(session);

        session.PhotoIds.Add("p1");
        session.PhotoIds.Add("p2");
        session.JobIds.Add("j1");
        session.JobIds.Add("j2");
        await store.UpdateAsync(session);

        var retrieved = await store.GetAsync("s1");
        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.PhotoIds.Count);
        Assert.Equal(2, retrieved.JobIds.Count);
        Assert.Contains("p1", retrieved.PhotoIds);
        Assert.Contains("j2", retrieved.JobIds);
    }

    [Fact]
    public async Task PersistsAcrossInstances()
    {
        var store1 = new JsonSessionStore(_tempDir);
        await store1.SaveAsync(new SessionRecord
        {
            SessionId = "persist1",
            Title = "persisted",
            Status = SessionStatus.Completed
        });

        var store2 = new JsonSessionStore(_tempDir);
        var retrieved = await store2.GetAsync("persist1");

        Assert.NotNull(retrieved);
        Assert.Equal("persist1", retrieved.SessionId);
        Assert.Equal("persisted", retrieved.Title);
        Assert.Equal(SessionStatus.Completed, retrieved.Status);
    }

    [Fact]
    public async Task Save_OverwritesExisting()
    {
        var store = new JsonSessionStore(_tempDir);
        await store.SaveAsync(new SessionRecord { SessionId = "s1", Title = "original" });
        await store.SaveAsync(new SessionRecord { SessionId = "s1", Title = "overwritten" });

        var all = await store.GetAllAsync();
        Assert.Single(all);

        var retrieved = await store.GetAsync("s1");
        Assert.NotNull(retrieved);
        Assert.Equal("overwritten", retrieved.Title);
    }
}
