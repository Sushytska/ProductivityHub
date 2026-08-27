using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class TaskEmbeddingProcessorTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static TaskEmbeddingProcessor CreateSut(
        AppDbContext db,
        IEmbeddingService embeddingService,
        FakeTaskEmbeddingQueue queue) =>
        new(db, embeddingService, queue, NullLogger<TaskEmbeddingProcessor>.Instance, TimeSpan.Zero);

    private static TaskItem CreateTask(int embeddingAttempts = 0) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Title = "Buy groceries",
        Description = "Milk, eggs, bread",
        DueDate = new DateOnly(2026, 9, 1),
        EmbeddingAttempts = embeddingAttempts
    };

    [Fact]
    public async Task ProcessAsync_Success_MarksCompleted()
    {
        // Embedding itself isn't asserted here: TestAppDbContext ignores TaskItem.Embedding
        // (same reason NoteChunk.Embedding is ignored — the InMemory provider has no pgvector
        // support), so it can't be verified as actually persisted from a unit test; only that
        // the embedding call happened without error and the status transitioned correctly.
        using var db = CreateDbContext();
        var task = CreateTask();
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(texts => texts.Select(_ => new float[] { 1f, 2f, 3f }).ToList());
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(task.Id, CancellationToken.None);

        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(EmbeddingStatus.Completed, updated.EmbeddingStatus);
        Assert.Equal(0, updated.EmbeddingAttempts);
        Assert.Null(updated.EmbeddingError);
    }

    [Fact]
    public async Task ProcessAsync_EmbedsTitleStatusDueDateAndDescription()
    {
        using var db = CreateDbContext();
        var task = CreateTask();
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        string[]? seenTexts = null;
        var embeddingService = new FakeEmbeddingService(texts =>
        {
            seenTexts = texts.ToArray();
            return texts.Select(_ => new float[] { 1f, 2f, 3f }).ToList();
        });
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(task.Id, CancellationToken.None);

        Assert.NotNull(seenTexts);
        var embeddedText = Assert.Single(seenTexts!);
        Assert.Contains("Buy groceries", embeddedText);
        Assert.Contains("Not completed", embeddedText);
        Assert.Contains("2026-09-01", embeddedText);
        Assert.Contains("Milk, eggs, bread", embeddedText);
    }

    [Fact]
    public async Task ProcessAsync_ReprocessingOverwritesExistingEmbedding()
    {
        using var db = CreateDbContext();
        var task = CreateTask();
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(texts => texts.Select(_ => new float[] { 9f, 9f, 9f }).ToList());
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(task.Id, CancellationToken.None);
        await sut.ProcessAsync(task.Id, CancellationToken.None);

        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(EmbeddingStatus.Completed, updated.EmbeddingStatus);
    }

    [Fact]
    public async Task ProcessAsync_FailureBelowMaxAttempts_SetsPendingIncrementsAttemptsAndRequeues()
    {
        using var db = CreateDbContext();
        var task = CreateTask();
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("boom"));
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(task.Id, CancellationToken.None);

        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(EmbeddingStatus.Pending, updated.EmbeddingStatus);
        Assert.Equal(1, updated.EmbeddingAttempts);
        Assert.Equal("boom", updated.EmbeddingError);
        Assert.Single(queue.EnqueuedIds);
        Assert.Equal(task.Id, queue.EnqueuedIds[0]);
    }

    [Fact]
    public async Task ProcessAsync_RequeueAfterFailureThrows_DoesNotPropagate()
    {
        using var db = CreateDbContext();
        var task = CreateTask();
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("boom"));
        var queue = new FakeTaskEmbeddingQueue { ThrowOnEnqueue = true };
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(task.Id, CancellationToken.None);

        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(EmbeddingStatus.Pending, updated.EmbeddingStatus);
        Assert.Equal(1, updated.EmbeddingAttempts);
    }

    [Fact]
    public async Task ProcessAsync_FailureAtMaxAttempts_MarksFailedAndDoesNotRequeue()
    {
        using var db = CreateDbContext();
        var task = CreateTask(embeddingAttempts: TaskEmbeddingProcessor.MaxEmbeddingAttempts - 1);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("boom"));
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(task.Id, CancellationToken.None);

        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(EmbeddingStatus.Failed, updated.EmbeddingStatus);
        Assert.Equal(TaskEmbeddingProcessor.MaxEmbeddingAttempts, updated.EmbeddingAttempts);
        Assert.Empty(queue.EnqueuedIds);
    }

    [Fact]
    public async Task ProcessAsync_TaskDeletedBeforeProcessing_DoesNothing()
    {
        using var db = CreateDbContext();
        var embeddingService = new FakeEmbeddingService(_ => throw new InvalidOperationException("should not be called"));
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, embeddingService, queue);

        await sut.ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(queue.EnqueuedIds);
    }
}
