using ProductivityHub.Services;

namespace ProductivityHub.Tests;

internal class FakeNoteEmbeddingQueue : INoteEmbeddingQueue
{
    public List<Guid> EnqueuedIds { get; } = new();

    public void Enqueue(Guid noteId) => EnqueuedIds.Add(noteId);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not needed for NoteService unit tests.");
}
