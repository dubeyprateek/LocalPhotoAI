using LocalPhotoAI.Shared.Queue;

namespace LocalPhotoAI.Tests;

public class InMemoryJobQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_ReturnsMessage()
    {
        var queue = new InMemoryJobQueue();
        var message = new QueueMessage { JobId = "j1", PhotoId = "p1", Pipeline = "default" };

        await queue.EnqueueAsync(message);
        var result = await queue.DequeueAsync();

        Assert.Equal("j1", result.JobId);
        Assert.Equal("p1", result.PhotoId);
    }

    [Fact]
    public async Task MultipleMessages_DequeuedInOrder()
    {
        var queue = new InMemoryJobQueue();

        await queue.EnqueueAsync(new QueueMessage { JobId = "j1", PhotoId = "p1" });
        await queue.EnqueueAsync(new QueueMessage { JobId = "j2", PhotoId = "p2" });
        await queue.EnqueueAsync(new QueueMessage { JobId = "j3", PhotoId = "p3" });

        var first = await queue.DequeueAsync();
        var second = await queue.DequeueAsync();
        var third = await queue.DequeueAsync();

        Assert.Equal("j1", first.JobId);
        Assert.Equal("j2", second.JobId);
        Assert.Equal("j3", third.JobId);
    }

    [Fact]
    public async Task CompetingConsumers_NoDuplicateProcessing()
    {
        var queue = new InMemoryJobQueue();
        var processedIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        for (int i = 0; i < 10; i++)
        {
            await queue.EnqueueAsync(new QueueMessage { JobId = $"j{i}", PhotoId = $"p{i}" });
        }

        // Simulate two competing consumers
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var consumer1 = ConsumeAsync(queue, processedIds, 5, cts.Token);
        var consumer2 = ConsumeAsync(queue, processedIds, 5, cts.Token);

        await Task.WhenAll(consumer1, consumer2);

        Assert.Equal(10, processedIds.Count);
        Assert.Equal(10, processedIds.Distinct().Count()); // No duplicates
    }

    private static async Task ConsumeAsync(
        IJobQueue queue,
        System.Collections.Concurrent.ConcurrentBag<string> bag,
        int count,
        CancellationToken token)
    {
        for (int i = 0; i < count; i++)
        {
            var msg = await queue.DequeueAsync(token);
            bag.Add(msg.JobId);
        }
    }
}
