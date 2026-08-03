using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;
using static ProductivityHub.DTOs.ChatDTOs;

namespace ProductivityHub.Tests;

public class ChatOrchestrationServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static NoteChunk CreateChunk(string noteTitle, string chunkText, int chunkIndex = 0)
    {
        var note = new Note { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = noteTitle, Content = "..." };
        return new NoteChunk { Id = Guid.NewGuid(), NoteId = note.Id, ChunkText = chunkText, ChunkIndex = chunkIndex, Note = note };
    }

    private static ChatOrchestrationService CreateSut(
        AppDbContext db, FakeRagService? ragService = null, FakeChatService? chatService = null) =>
        new(
            db,
            ragService ?? new FakeRagService((_, _, _) => Array.Empty<NoteChunk>()),
            chatService ?? new FakeChatService(),
            NullLogger<ChatOrchestrationService>.Instance);

    [Fact]
    public async Task AskAsync_PersistsUserQuestionAndAssistantAnswer()
    {
        using var db = CreateDbContext();
        var chatService = new FakeChatService { Answer = "The answer." };
        var sut = CreateSut(db, chatService: chatService);
        var userId = Guid.NewGuid();

        await sut.AskAsync(userId, new ChatRequest("What is X?"), CancellationToken.None);

        var messages = await db.ChatMessages.Where(m => m.UserId == userId).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Role == ChatRoles.User && m.Message == "What is X?");
        Assert.Contains(messages, m => m.Role == ChatRoles.Assistant && m.Message == "The answer.");
    }

    [Fact]
    public async Task AskAsync_ReturnsAnswerAndSourcesFromRetrievedChunks()
    {
        using var db = CreateDbContext();
        var chunk = CreateChunk("My Note", "chunk text", chunkIndex: 2);
        var ragService = new FakeRagService((_, _, _) => new[] { chunk });
        var chatService = new FakeChatService { Answer = "Grounded answer." };
        var sut = CreateSut(db, ragService, chatService);

        var response = await sut.AskAsync(Guid.NewGuid(), new ChatRequest("What is X?"), CancellationToken.None);

        Assert.Equal("Grounded answer.", response.Answer);
        Assert.Single(response.Sources);
        Assert.Equal(chunk.NoteId, response.Sources[0].NoteId);
        Assert.Equal("My Note", response.Sources[0].NoteTitle);
        Assert.Equal(2, response.Sources[0].ChunkIndex);
    }

    [Fact]
    public async Task AskAsync_ScopesRetrievalToCallingUser()
    {
        using var db = CreateDbContext();
        Guid? seenUserId = null;
        var ragService = new FakeRagService((userId, _, _) =>
        {
            seenUserId = userId;
            return Array.Empty<NoteChunk>();
        });
        var sut = CreateSut(db, ragService: ragService);
        var callingUser = Guid.NewGuid();

        await sut.AskAsync(callingUser, new ChatRequest("What is X?"), CancellationToken.None);

        Assert.Equal(callingUser, seenUserId);
    }
}
