using ProductivityHub.Services;

namespace ProductivityHub.Tests;

internal class FakeTaskEmbeddingQueue : ITaskEmbeddingQueue
{
    public List<Guid> EnqueuedIds { get; } = new();
    public bool ThrowOnEnqueue { get; set; }

    public void Enqueue(Guid taskId)
    {
        if (ThrowOnEnqueue)
        {
            throw new InvalidOperationException("Simulated queue failure.");
        }

        EnqueuedIds.Add(taskId);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not needed for TaskService unit tests.");
}
