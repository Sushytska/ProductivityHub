using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;
using static ProductivityHub.DTOs.TaskDTOs;

namespace ProductivityHub.Tests;

public class TaskServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static TaskService CreateSut(AppDbContext db, FakeTaskEmbeddingQueue? queue = null) =>
        new(db, queue ?? new FakeTaskEmbeddingQueue(), NullLogger<TaskService>.Instance);

    private static CreateTaskRequest Request(
        string title = "Title", string? description = "Description", bool isCompleted = false, DateOnly? dueDate = null) =>
        new(title, description, isCompleted, dueDate);

    [Fact]
    public async Task CreateAsync_SetsOwnerToCallingUser()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();

        var response = await sut.CreateAsync(userId, Request("Title", "Description"));

        var stored = await db.Tasks.SingleAsync(t => t.Id == response.Id);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("Title", stored.Title);
        Assert.Equal("Description", stored.Description);
    }

    [Fact]
    public async Task CreateAsync_EnqueuesTaskIdForEmbedding()
    {
        using var db = CreateDbContext();
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, queue);

        var response = await sut.CreateAsync(Guid.NewGuid(), Request());

        Assert.Single(queue.EnqueuedIds);
        Assert.Equal(response.Id, queue.EnqueuedIds[0]);
    }

    [Fact]
    public async Task CreateAsync_DefaultsIsCompletedToFalse()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var response = await sut.CreateAsync(Guid.NewGuid(), Request(isCompleted: false));

        Assert.False(response.IsCompleted);
    }

    [Fact]
    public async Task CreateAsync_PersistsDueDate()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var dueDate = new DateOnly(2026, 9, 1);

        var response = await sut.CreateAsync(Guid.NewGuid(), Request(dueDate: dueDate));

        Assert.Equal(dueDate, response.DueDate);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyCallingUsersTasks()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await sut.CreateAsync(userA, Request("A1"));
        await sut.CreateAsync(userA, Request("A2"));
        await sut.CreateAsync(userB, Request("B1"));

        var result = await sut.GetAllAsync(userA);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Contains(t.Title, new[] { "A1", "A2" }));
    }

    [Fact]
    public async Task GetAllAsync_OrdersIncompleteFirstThenNewest()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();

        var first = await sut.CreateAsync(userId, Request("First"));
        var second = await sut.CreateAsync(userId, Request("Second"));
        var third = await sut.CreateAsync(userId, Request("Third"));

        // Mark the newest one completed — it should sort after the two incomplete tasks
        // despite being the most recently created.
        await sut.UpdateAsync(userId, third.Id, new UpdateTaskRequest("Third", null, true, null));

        var result = await sut.GetAllAsync(userId);

        Assert.Equal(new[] { "Second", "First", "Third" }, result.Select(t => t.Title));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOwnTask()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request("Mine"));

        var result = await sut.GetByIdAsync(userId, created.Id);

        Assert.NotNull(result);
        Assert.Equal("Mine", result!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissingTask()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnotherUsersTask()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, Request("Private"));

        var result = await sut.GetByIdAsync(intruder, created.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesOwnTask()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request("Old", "Old desc"));
        var dueDate = new DateOnly(2026, 12, 25);

        var result = await sut.UpdateAsync(userId, created.Id, new UpdateTaskRequest("New", "New desc", true, dueDate));

        Assert.NotNull(result);
        Assert.Equal("New", result!.Title);
        Assert.Equal("New desc", result.Description);
        Assert.True(result.IsCompleted);
        Assert.Equal(dueDate, result.DueDate);

        var stored = await db.Tasks.SingleAsync(t => t.Id == created.Id);
        Assert.Equal("New", stored.Title);
        Assert.True(stored.IsCompleted);
    }

    [Fact]
    public async Task UpdateAsync_OwnTask_ResetsEmbeddingStatusAndEnqueues()
    {
        using var db = CreateDbContext();
        var queue = new FakeTaskEmbeddingQueue();
        var sut = CreateSut(db, queue);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request("Old", "Old desc"));

        var stored = await db.Tasks.SingleAsync(t => t.Id == created.Id);
        stored.EmbeddingStatus = EmbeddingStatus.Failed;
        stored.EmbeddingAttempts = 3;
        stored.EmbeddingError = "boom";
        await db.SaveChangesAsync();
        queue.EnqueuedIds.Clear(); // ignore the enqueue from CreateAsync

        await sut.UpdateAsync(userId, created.Id, new UpdateTaskRequest("New", "New desc", false, null));

        var updated = await db.Tasks.SingleAsync(t => t.Id == created.Id);
        Assert.Equal(EmbeddingStatus.Pending, updated.EmbeddingStatus);
        Assert.Equal(0, updated.EmbeddingAttempts);
        Assert.Null(updated.EmbeddingError);
        Assert.Single(queue.EnqueuedIds);
        Assert.Equal(created.Id, queue.EnqueuedIds[0]);
    }

    [Fact]
    public async Task UpdateAsync_CanClearDueDate()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request(dueDate: new DateOnly(2026, 9, 1)));

        var result = await sut.UpdateAsync(userId, created.Id, new UpdateTaskRequest("Title", "Description", false, null));

        Assert.Null(result!.DueDate);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForMissingTask()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateTaskRequest("X", null, false, null));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForAnotherUsersTaskAndDoesNotModify()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, Request("Original"));

        var result = await sut.UpdateAsync(intruder, created.Id, new UpdateTaskRequest("Hacked", null, true, null));

        Assert.Null(result);
        var stored = await db.Tasks.SingleAsync(t => t.Id == created.Id);
        Assert.Equal("Original", stored.Title);
        Assert.False(stored.IsCompleted);
    }

    [Fact]
    public async Task DeleteAsync_DeletesOwnTask()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request("ToDelete"));

        var result = await sut.DeleteAsync(userId, created.Id);

        Assert.True(result);
        Assert.False(await db.Tasks.AnyAsync(t => t.Id == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForMissingTask()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForAnotherUsersTaskAndDoesNotDelete()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, Request("Keep"));

        var result = await sut.DeleteAsync(intruder, created.Id);

        Assert.False(result);
        Assert.True(await db.Tasks.AnyAsync(t => t.Id == created.Id));
    }
}
