namespace ProductivityHub.Services
{
    public interface ITaskEmbeddingQueue
    {
        void Enqueue(Guid taskId);

        IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
    }
}
