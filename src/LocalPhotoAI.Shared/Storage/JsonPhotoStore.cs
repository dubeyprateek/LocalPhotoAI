using System.Collections.Concurrent;
using System.Text.Json;
using LocalPhotoAI.Shared.Models;

namespace LocalPhotoAI.Shared.Storage;

/// <summary>
/// File-backed photo metadata store. Persists data across restarts.
/// </summary>
public class JsonPhotoStore : IPhotoStore
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, PhotoMetadata> _photos = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonPhotoStore(string storagePath)
    {
        Directory.CreateDirectory(storagePath);
        _filePath = Path.Combine(storagePath, "photos.json");
        Load();
    }

    public Task SaveAsync(PhotoMetadata photo)
    {
        _photos[photo.PhotoId] = photo;
        return PersistAsync();
    }

    public Task<PhotoMetadata?> GetAsync(string photoId)
    {
        _photos.TryGetValue(photoId, out var photo);
        return Task.FromResult(photo);
    }

    public Task<IReadOnlyList<PhotoMetadata>> GetAllAsync()
    {
        IReadOnlyList<PhotoMetadata> list = _photos.Values.ToList();
        return Task.FromResult(list);
    }

    public Task UpdateAsync(PhotoMetadata photo)
    {
        _photos[photo.PhotoId] = photo;
        return PersistAsync();
    }

    public Task<bool> DeleteAsync(string photoId)
    {
        var removed = _photos.TryRemove(photoId, out _);
        if (removed) return PersistAsync().ContinueWith(_ => true);
        return Task.FromResult(false);
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        var json = File.ReadAllText(_filePath);
        var list = JsonSerializer.Deserialize<List<PhotoMetadata>>(json) ?? [];
        foreach (var p in list)
            _photos[p.PhotoId] = p;
    }

    private async Task PersistAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_photos.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }
}
