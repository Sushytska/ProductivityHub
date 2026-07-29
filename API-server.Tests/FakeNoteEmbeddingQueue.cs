using ProductivityHub.Services;

namespace ProductivityHub.Tests;

internal class FakeNoteEmbeddingQueue : INoteEmbeddingQueue
{
    public List<Guid> EnqueuedIds { get; } = new();
    public bool ThrowOnEnqueue { get; set; }

    public void Enqueue(Guid noteId)
    {
        if (ThrowOnEnqueue)
        {
            throw new InvalidOperationException("Simulated queue failure.");
        }

        EnqueuedIds.Add(noteId);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not needed for NoteService unit tests.");
}
