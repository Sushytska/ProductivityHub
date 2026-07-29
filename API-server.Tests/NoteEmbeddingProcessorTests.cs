using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class NoteEmbeddingProcessorTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static NoteEmbeddingProcessor CreateSut(
        AppDbContext db,
        IEmbeddingService embeddingService,
        FakeNoteEmbeddingQueue queue) =>
        new(db, new NoteChunker(), embeddingService, queue, NullLogger<NoteEmbeddingProcessor>.Instance, TimeSpan.Zero);

    private static Note CreateNote(int embeddingAttempts = 0) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Title = "Title",
        Content = "hello world",
        EmbeddingAttempts = embeddingAttempts
    };

    [Fact]
    public async Task ProcessAsync_Success_MarksCompletedAndStoresChunks()
    {
        using var db = CreateDbContext();
        var note = CreateNote();
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(texts => texts.Select(_ => new float[] { 1f, 2f, 3f }).ToList());
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(note.Id, CancellationToken.None);

        var updated = await db.Notes.SingleAsync(n => n.Id == note.Id);
        Assert.Equal(EmbeddingStatus.Completed, updated.EmbeddingStatus);
        Assert.Equal(0, updated.EmbeddingAttempts);
        Assert.Null(updated.EmbeddingError);

        var chunks = await db.NoteChunks.Where(c => c.NoteId == note.Id).OrderBy(c => c.ChunkIndex).ToListAsync();
        Assert.Single(chunks);
        Assert.Equal("hello world", chunks[0].ChunkText);
        Assert.Equal(0, chunks[0].ChunkIndex);
    }

    [Fact]
    public async Task ProcessAsync_ReplacesExistingChunksOnReprocessing()
    {
        using var db = CreateDbContext();
        var note = CreateNote();
        db.Notes.Add(note);
        db.NoteChunks.Add(new NoteChunk { Id = Guid.NewGuid(), NoteId = note.Id, ChunkText = "stale", ChunkIndex = 0 });
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(texts => texts.Select(_ => new float[] { 1f, 2f, 3f }).ToList());
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(note.Id, CancellationToken.None);

        var chunks = await db.NoteChunks.Where(c => c.NoteId == note.Id).ToListAsync();
        Assert.Single(chunks);
        Assert.Equal("hello world", chunks[0].ChunkText);
    }

    [Fact]
    public async Task ProcessAsync_FailureBelowMaxAttempts_SetsPendingIncrementsAttemptsAndRequeues()
    {
        using var db = CreateDbContext();
        var note = CreateNote();
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("boom"));
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(note.Id, CancellationToken.None);

        var updated = await db.Notes.SingleAsync(n => n.Id == note.Id);
        Assert.Equal(EmbeddingStatus.Pending, updated.EmbeddingStatus);
        Assert.Equal(1, updated.EmbeddingAttempts);
        Assert.Equal("boom", updated.EmbeddingError);
        Assert.Single(queue.EnqueuedIds);
        Assert.Equal(note.Id, queue.EnqueuedIds[0]);
    }

    [Fact]
    public async Task ProcessAsync_RequeueAfterFailureThrows_DoesNotPropagate()
    {
        using var db = CreateDbContext();
        var note = CreateNote();
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("boom"));
        var queue = new FakeNoteEmbeddingQueue { ThrowOnEnqueue = true };
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(note.Id, CancellationToken.None);

        var updated = await db.Notes.SingleAsync(n => n.Id == note.Id);
        Assert.Equal(EmbeddingStatus.Pending, updated.EmbeddingStatus);
        Assert.Equal(1, updated.EmbeddingAttempts);
    }

    [Fact]
    public async Task ProcessAsync_FailureAtMaxAttempts_MarksFailedAndDoesNotRequeue()
    {
        using var db = CreateDbContext();
        var note = CreateNote(embeddingAttempts: NoteEmbeddingProcessor.MaxEmbeddingAttempts - 1);
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("boom"));
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(note.Id, CancellationToken.None);

        var updated = await db.Notes.SingleAsync(n => n.Id == note.Id);
        Assert.Equal(EmbeddingStatus.Failed, updated.EmbeddingStatus);
        Assert.Equal(NoteEmbeddingProcessor.MaxEmbeddingAttempts, updated.EmbeddingAttempts);
        Assert.Empty(queue.EnqueuedIds);
    }

    [Fact]
    public async Task ProcessAsync_NoteDeletedBeforeProcessing_DoesNothing()
    {
        using var db = CreateDbContext();
        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("should not be called"));
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(queue.EnqueuedIds);
    }
}
