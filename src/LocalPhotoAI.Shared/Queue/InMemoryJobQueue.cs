using System.Threading.Channels;

namespace LocalPhotoAI.Shared.Queue;

/// <summary>
/// In-process unbounded channel-based job queue.
/// Supports multiple producers and competing consumers.
/// </summary>
public class InMemoryJobQueue : IJobQueue
{
    private readonly Channel<QueueMessage> _channel = Channel.CreateUnbounded<QueueMessage>(
        new UnboundedChannelOptions { SingleReader = false });

    public ValueTask EnqueueAsync(QueueMessage message, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(message, cancellationToken);

    public ValueTask<QueueMessage> DequeueAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken);
}
