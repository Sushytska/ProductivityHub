using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class StrandedNoteReconcilerTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static Note CreateNote(EmbeddingStatus status) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Title = "Title",
        Content = "Content",
        EmbeddingStatus = status
    };

    [Fact]
    public async Task RequeueStrandedNotesAsync_PendingAndProcessingNotes_AreEnqueued()
    {
        using var db = CreateDbContext();
        var pending = CreateNote(EmbeddingStatus.Pending);
        var processing = CreateNote(EmbeddingStatus.Processing);
        db.Notes.AddRange(pending, processing);
        await db.SaveChangesAsync();

        var queue = new FakeNoteEmbeddingQueue();
        var sut = new StrandedNoteReconciler(db, queue, NullLogger<StrandedNoteReconciler>.Instance);

        await sut.RequeueStrandedNotesAsync();

        Assert.Equal(2, queue.EnqueuedIds.Count);
        Assert.Contains(pending.Id, queue.EnqueuedIds);
        Assert.Contains(processing.Id, queue.EnqueuedIds);
    }

    [Fact]
    public async Task RequeueStrandedNotesAsync_CompletedAndFailedNotes_AreNotEnqueued()
    {
        using var db = CreateDbContext();
        var completed = CreateNote(EmbeddingStatus.Completed);
        var failed = CreateNote(EmbeddingStatus.Failed);
        db.Notes.AddRange(completed, failed);
        await db.SaveChangesAsync();

        var queue = new FakeNoteEmbeddingQueue();
        var sut = new StrandedNoteReconciler(db, queue, NullLogger<StrandedNoteReconciler>.Instance);

        await sut.RequeueStrandedNotesAsync();

        Assert.Empty(queue.EnqueuedIds);
    }

    [Fact]
    public async Task RequeueStrandedNotesAsync_NoNotes_DoesNothing()
    {
        using var db = CreateDbContext();
        var queue = new FakeNoteEmbeddingQueue();
        var sut = new StrandedNoteReconciler(db, queue, NullLogger<StrandedNoteReconciler>.Instance);

        await sut.RequeueStrandedNotesAsync();

        Assert.Empty(queue.EnqueuedIds);
    }
}
