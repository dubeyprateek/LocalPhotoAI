using LocalPhotoAI.Shared.Models;

namespace LocalPhotoAI.Shared.Storage;

public interface IJobStore
{
    Task SaveAsync(JobRecord job);
    Task<JobRecord?> GetAsync(string jobId);
    Task<IReadOnlyList<JobRecord>> GetAllAsync();
    Task UpdateAsync(JobRecord job);
    Task<bool> DeleteAsync(string jobId);
}
