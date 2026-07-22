using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;
using static ProductivityHub.DTOs.NoteDTOs;

namespace ProductivityHub.Tests;

public class NoteServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static NoteService CreateSut(AppDbContext db, FakeNoteEmbeddingQueue? queue = null) =>
        new(db, queue ?? new FakeNoteEmbeddingQueue());

    [Fact]
    public async Task CreateAsync_SetsOwnerToCallingUser()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();

        var response = await sut.CreateAsync(userId, new CreateNoteRequest("Title", "Content"));

        var stored = await db.Notes.SingleAsync(n => n.Id == response.Id);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("Title", stored.Title);
        Assert.Equal("Content", stored.Content);
        Assert.Equal(EmbeddingStatus.Pending, stored.EmbeddingStatus);
    }

    [Fact]
    public async Task CreateAsync_EnqueuesNoteIdForEmbedding()
    {
        using var db = CreateDbContext();
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, queue);

        var response = await sut.CreateAsync(Guid.NewGuid(), new CreateNoteRequest("Title", "Content"));

        Assert.Single(queue.EnqueuedIds);
        Assert.Equal(response.Id, queue.EnqueuedIds[0]);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyCallingUsersNotes()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await sut.CreateAsync(userA, new CreateNoteRequest("A1", "..."));
        await sut.CreateAsync(userA, new CreateNoteRequest("A2", "..."));
        await sut.CreateAsync(userB, new CreateNoteRequest("B1", "..."));

        var result = await sut.GetAllAsync(userA);

        Assert.Equal(2, result.Count);
        Assert.All(result, n => Assert.Contains(n.Title, new[] { "A1", "A2" }));
    }

    [Fact]
    public async Task GetByIdAsync_OwnNote_ReturnsNote()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, new CreateNoteRequest("Mine", "Body"));

        var result = await sut.GetByIdAsync(userId, created.Id);

        Assert.NotNull(result);
        Assert.Equal("Mine", result!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NoteDoesNotExist_ReturnsNull()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_NoteBelongsToAnotherUser_ReturnsNull()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, new CreateNoteRequest("Private", "Body"));

        var result = await sut.GetByIdAsync(intruder, created.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_OwnNote_UpdatesAndReturnsNote()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, new CreateNoteRequest("Old", "Old body"));

        var result = await sut.UpdateAsync(userId, created.Id, new UpdateNoteRequest("New", "New body"));

        Assert.NotNull(result);
        Assert.Equal("New", result!.Title);
        Assert.Equal("New body", result.Content);

        var stored = await db.Notes.SingleAsync(n => n.Id == created.Id);
        Assert.Equal("New", stored.Title);
    }

    [Fact]
    public async Task UpdateAsync_OwnNote_ResetsEmbeddingStatusToPending()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, new CreateNoteRequest("Old", "Old body"));

        var stored = await db.Notes.SingleAsync(n => n.Id == created.Id);
        stored.EmbeddingStatus = EmbeddingStatus.Failed;
        stored.EmbeddingAttempts = 3;
        stored.EmbeddingError = "boom";
        await db.SaveChangesAsync();

        await sut.UpdateAsync(userId, created.Id, new UpdateNoteRequest("New", "New body"));

        var updated = await db.Notes.SingleAsync(n => n.Id == created.Id);
        Assert.Equal(EmbeddingStatus.Pending, updated.EmbeddingStatus);
        Assert.Equal(0, updated.EmbeddingAttempts);
        Assert.Null(updated.EmbeddingError);
    }

    [Fact]
    public async Task UpdateAsync_ExistingNote_EnqueuesNoteIdForEmbedding()
    {
        using var db = CreateDbContext();
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, queue);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, new CreateNoteRequest("Old", "Old body"));
        queue.EnqueuedIds.Clear(); // ignore the enqueue from CreateAsync

        await sut.UpdateAsync(userId, created.Id, new UpdateNoteRequest("New", "New body"));

        Assert.Single(queue.EnqueuedIds);
        Assert.Equal(created.Id, queue.EnqueuedIds[0]);
    }

    [Fact]
    public async Task UpdateAsync_NoteDoesNotExist_ReturnsNull()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateNoteRequest("X", "Y"));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_NoteDoesNotExist_DoesNotEnqueue()
    {
        using var db = CreateDbContext();
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, queue);

        await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateNoteRequest("X", "Y"));

        Assert.Empty(queue.EnqueuedIds);
    }

    [Fact]
    public async Task UpdateAsync_NoteBelongsToAnotherUser_ReturnsNullAndDoesNotModify()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, new CreateNoteRequest("Original", "Body"));

        var result = await sut.UpdateAsync(intruder, created.Id, new UpdateNoteRequest("Hacked", "Hacked"));

        Assert.Null(result);
        var stored = await db.Notes.SingleAsync(n => n.Id == created.Id);
        Assert.Equal("Original", stored.Title);
    }

    [Fact]
    public async Task UpdateAsync_NoteBelongsToAnotherUser_DoesNotEnqueue()
    {
        using var db = CreateDbContext();
        var queue = new FakeNoteEmbeddingQueue();
        var sut = CreateSut(db, queue);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, new CreateNoteRequest("Private", "Body"));
        queue.EnqueuedIds.Clear();

        await sut.UpdateAsync(intruder, created.Id, new UpdateNoteRequest("Hacked", "Hacked"));

        Assert.Empty(queue.EnqueuedIds);
    }

    [Fact]
    public async Task DeleteAsync_OwnNote_RemovesNoteAndReturnsTrue()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, new CreateNoteRequest("ToDelete", "Body"));

        var result = await sut.DeleteAsync(userId, created.Id);

        Assert.True(result);
        Assert.False(await db.Notes.AnyAsync(n => n.Id == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_NoteDoesNotExist_ReturnsFalse()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_NoteBelongsToAnotherUser_ReturnsFalseAndDoesNotDelete()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, new CreateNoteRequest("Keep", "Body"));

        var result = await sut.DeleteAsync(intruder, created.Id);

        Assert.False(result);
        Assert.True(await db.Notes.AnyAsync(n => n.Id == created.Id));
    }
}
