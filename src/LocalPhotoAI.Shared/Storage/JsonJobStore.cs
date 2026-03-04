using System.Collections.Concurrent;
using System.Text.Json;
using LocalPhotoAI.Shared.Models;

namespace LocalPhotoAI.Shared.Storage;

/// <summary>
/// File-backed job metadata store. Persists data across restarts.
/// </summary>
public class JsonJobStore : IJobStore
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, JobRecord> _jobs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonJobStore(string storagePath)
    {
        Directory.CreateDirectory(storagePath);
        _filePath = Path.Combine(storagePath, "jobs.json");
        Load();
    }

    public Task SaveAsync(JobRecord job)
    {
        _jobs[job.JobId] = job;
        return PersistAsync();
    }

    public Task<JobRecord?> GetAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<JobRecord>> GetAllAsync()
    {
        IReadOnlyList<JobRecord> list = _jobs.Values.ToList();
        return Task.FromResult(list);
    }

    public Task UpdateAsync(JobRecord job)
    {
        _jobs[job.JobId] = job;
        return PersistAsync();
    }

    public Task<bool> DeleteAsync(string jobId)
    {
        var removed = _jobs.TryRemove(jobId, out _);
        if (removed) return PersistAsync().ContinueWith(_ => true);
        return Task.FromResult(false);
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        var json = File.ReadAllText(_filePath);
        var list = JsonSerializer.Deserialize<List<JobRecord>>(json) ?? [];
        foreach (var j in list)
            _jobs[j.JobId] = j;
    }

    private async Task PersistAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_jobs.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }
}
