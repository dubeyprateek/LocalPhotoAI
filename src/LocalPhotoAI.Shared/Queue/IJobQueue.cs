namespace LocalPhotoAI.Shared.Queue;

public interface IJobQueue
{
    ValueTask EnqueueAsync(QueueMessage message, CancellationToken cancellationToken = default);
    ValueTask<QueueMessage> DequeueAsync(CancellationToken cancellationToken = default);
}
