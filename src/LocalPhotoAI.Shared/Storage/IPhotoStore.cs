using LocalPhotoAI.Shared.Models;

namespace LocalPhotoAI.Shared.Storage;

public interface IPhotoStore
{
    Task SaveAsync(PhotoMetadata photo);
    Task<PhotoMetadata?> GetAsync(string photoId);
    Task<IReadOnlyList<PhotoMetadata>> GetAllAsync();
    Task UpdateAsync(PhotoMetadata photo);
    Task<bool> DeleteAsync(string photoId);
}
