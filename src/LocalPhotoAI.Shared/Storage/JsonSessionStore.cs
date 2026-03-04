using System.Collections.Concurrent;
using System.Text.Json;
using LocalPhotoAI.Shared.Models;

namespace LocalPhotoAI.Shared.Storage;

/// <summary>
/// File-backed session metadata store. Persists data across restarts.
/// </summary>
public class JsonSessionStore : ISessionStore
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonSessionStore(string storagePath)
    {
        Directory.CreateDirectory(storagePath);
        _filePath = Path.Combine(storagePath, "sessions.json");
        Load();
    }

    public Task SaveAsync(SessionRecord session)
    {
        _sessions[session.SessionId] = session;
        return PersistAsync();
    }

    public Task<SessionRecord?> GetAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<SessionRecord>> GetAllAsync()
    {
        IReadOnlyList<SessionRecord> list = _sessions.Values.ToList();
        return Task.FromResult(list);
    }

    public Task UpdateAsync(SessionRecord session)
    {
        _sessions[session.SessionId] = session;
        return PersistAsync();
    }

    public Task<bool> DeleteAsync(string sessionId)
    {
        var removed = _sessions.TryRemove(sessionId, out _);
        if (removed) return PersistAsync().ContinueWith(_ => true);
        return Task.FromResult(false);
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        var json = File.ReadAllText(_filePath);
        var list = JsonSerializer.Deserialize<List<SessionRecord>>(json) ?? [];
        foreach (var s in list)
            _sessions[s.SessionId] = s;
    }

    private async Task PersistAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_sessions.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }
}
