using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class StrandedTaskReconcilerTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static TaskItem CreateTask(EmbeddingStatus status) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Title = "Title",
        EmbeddingStatus = status
    };

    [Fact]
    public async Task RequeueStrandedTasksAsync_PendingAndProcessingTasks_AreEnqueued()
    {
        using var db = CreateDbContext();
        var pending = CreateTask(EmbeddingStatus.Pending);
        var processing = CreateTask(EmbeddingStatus.Processing);
        db.Tasks.AddRange(pending, processing);
        await db.SaveChangesAsync();

        var queue = new FakeTaskEmbeddingQueue();
        var sut = new StrandedTaskReconciler(db, queue, NullLogger<StrandedTaskReconciler>.Instance);

        await sut.RequeueStrandedTasksAsync();

        Assert.Equal(2, queue.EnqueuedIds.Count);
        Assert.Contains(pending.Id, queue.EnqueuedIds);
        Assert.Contains(processing.Id, queue.EnqueuedIds);
    }

    [Fact]
    public async Task RequeueStrandedTasksAsync_CompletedAndFailedTasks_AreNotEnqueued()
    {
        using var db = CreateDbContext();
        var completed = CreateTask(EmbeddingStatus.Completed);
        var failed = CreateTask(EmbeddingStatus.Failed);
        db.Tasks.AddRange(completed, failed);
        await db.SaveChangesAsync();

        var queue = new FakeTaskEmbeddingQueue();
        var sut = new StrandedTaskReconciler(db, queue, NullLogger<StrandedTaskReconciler>.Instance);

        await sut.RequeueStrandedTasksAsync();

        Assert.Empty(queue.EnqueuedIds);
    }

    [Fact]
    public async Task RequeueStrandedTasksAsync_NoTasks_DoesNothing()
    {
        using var db = CreateDbContext();
        var queue = new FakeTaskEmbeddingQueue();
        var sut = new StrandedTaskReconciler(db, queue, NullLogger<StrandedTaskReconciler>.Instance);

        await sut.RequeueStrandedTasksAsync();

        Assert.Empty(queue.EnqueuedIds);
    }
}
