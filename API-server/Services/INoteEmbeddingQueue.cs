namespace ProductivityHub.Services
{
    public interface INoteEmbeddingQueue
    {
        void Enqueue(Guid noteId);

        IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
    }
}
