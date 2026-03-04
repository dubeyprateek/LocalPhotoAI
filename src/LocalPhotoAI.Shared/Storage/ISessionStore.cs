using LocalPhotoAI.Shared.Models;

namespace LocalPhotoAI.Shared.Storage;

public interface ISessionStore
{
    Task SaveAsync(SessionRecord session);
    Task<SessionRecord?> GetAsync(string sessionId);
    Task<IReadOnlyList<SessionRecord>> GetAllAsync();
    Task UpdateAsync(SessionRecord session);
}
