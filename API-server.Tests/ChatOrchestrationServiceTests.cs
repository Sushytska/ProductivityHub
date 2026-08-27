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

    private static RagSourceItem CreateSourceItem(string title, string text, int chunkIndex = 0, string sourceType = "Note") =>
        new(sourceType, Guid.NewGuid(), title, text, chunkIndex, Distance: 0.0);

    private static ChatOrchestrationService CreateSut(
        AppDbContext db, FakeRagService? ragService = null, FakeChatService? chatService = null) =>
        new(
            db,
            ragService ?? new FakeRagService((_, _, _) => Array.Empty<RagSourceItem>()),
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
        var item = CreateSourceItem("My Note", "chunk text", chunkIndex: 2);
        var ragService = new FakeRagService((_, _, _) => new[] { item });
        var chatService = new FakeChatService { Answer = "Grounded answer." };
        var sut = CreateSut(db, ragService, chatService);

        var response = await sut.AskAsync(Guid.NewGuid(), new ChatRequest("What is X?"), CancellationToken.None);

        Assert.Equal("Grounded answer.", response.Answer);
        Assert.Single(response.Sources);
        Assert.Equal(item.SourceId, response.Sources[0].SourceId);
        Assert.Equal("My Note", response.Sources[0].SourceTitle);
        Assert.Equal("Note", response.Sources[0].SourceType);
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
            return Array.Empty<RagSourceItem>();
        });
        var sut = CreateSut(db, ragService: ragService);
        var callingUser = Guid.NewGuid();

        await sut.AskAsync(callingUser, new ChatRequest("What is X?"), CancellationToken.None);

        Assert.Equal(callingUser, seenUserId);
    }

    private sealed class ThrowingSaveDbContext : TestAppDbContext
    {
        public ThrowingSaveDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated save failure.");
    }

    [Fact]
    public async Task AskStreamingAsync_YieldsMetaEventFirstWithRetrievedSources()
    {
        using var db = CreateDbContext();
        var item = CreateSourceItem("My Note", "chunk text", chunkIndex: 3);
        var ragService = new FakeRagService((_, _, _) => new[] { item });
        var sut = CreateSut(db, ragService: ragService);

        var events = new List<ChatStreamEvent>();
        await foreach (var evt in sut.AskStreamingAsync(Guid.NewGuid(), new ChatRequest("What is X?"), CancellationToken.None))
        {
            events.Add(evt);
        }

        var meta = Assert.IsType<ChatStreamEvent.Meta>(events[0]);
        Assert.Single(meta.Sources);
        Assert.Equal(item.SourceId, meta.Sources[0].SourceId);
        Assert.Equal("My Note", meta.Sources[0].SourceTitle);
        Assert.Equal(3, meta.Sources[0].ChunkIndex);
    }

    [Fact]
    public async Task AskStreamingAsync_YieldsOneTokenPerStreamChunkInOrder()
    {
        using var db = CreateDbContext();
        var chatService = new FakeChatService { StreamTokens = new[] { "Hel", "lo", " world" } };
        var sut = CreateSut(db, chatService: chatService);

        var events = new List<ChatStreamEvent>();
        await foreach (var evt in sut.AskStreamingAsync(Guid.NewGuid(), new ChatRequest("What is X?"), CancellationToken.None))
        {
            events.Add(evt);
        }

        var tokens = events.OfType<ChatStreamEvent.Token>().Select(t => t.Text).ToList();
        Assert.Equal(new[] { "Hel", "lo", " world" }, tokens);
    }

    [Fact]
    public async Task AskStreamingAsync_PersistsMessagesOnlyAfterFullEnumeration_ThenYieldsDone()
    {
        using var db = CreateDbContext();
        var chatService = new FakeChatService { StreamTokens = new[] { "Hel", "lo" } };
        var sut = CreateSut(db, chatService: chatService);
        var userId = Guid.NewGuid();

        await using var enumerator = sut.AskStreamingAsync(userId, new ChatRequest("Q?"), CancellationToken.None)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<ChatStreamEvent.Meta>(enumerator.Current);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<ChatStreamEvent.Token>(enumerator.Current);
        Assert.Empty(await db.ChatMessages.ToListAsync()); // nothing persisted mid-stream

        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<ChatStreamEvent.Token>(enumerator.Current);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<ChatStreamEvent.Done>(enumerator.Current);

        Assert.False(await enumerator.MoveNextAsync());

        var messages = await db.ChatMessages.Where(m => m.UserId == userId).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Role == ChatRoles.User && m.Message == "Q?");
        Assert.Contains(messages, m => m.Role == ChatRoles.Assistant && m.Message == "Hello");
    }

    [Fact]
    public async Task AskStreamingAsync_ClientCancelsMidStream_SkipsPersistence()
    {
        using var db = CreateDbContext();
        var chatService = new FakeChatService { StreamTokens = new[] { "Hel", "lo", " world" } };
        var sut = CreateSut(db, chatService: chatService);
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        await using var enumerator = sut.AskStreamingAsync(userId, new ChatRequest("Q?"), cts.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<ChatStreamEvent.Meta>(enumerator.Current);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<ChatStreamEvent.Token>(enumerator.Current);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());

        // No partial save: a cancelled/disconnected client must not leave a truncated
        // answer persisted, unlike the SaveChangesAsync-fails case which does persist
        // the user question and yields an Error event instead.
        Assert.Empty(await db.ChatMessages.Where(m => m.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task AskStreamingAsync_PersistenceFails_YieldsErrorInsteadOfDone_WithoutErasingPriorTokens()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new ThrowingSaveDbContext(options);
        var chatService = new FakeChatService { StreamTokens = new[] { "Hi" } };
        var sut = CreateSut(db, chatService: chatService);

        var events = new List<ChatStreamEvent>();
        await foreach (var evt in sut.AskStreamingAsync(Guid.NewGuid(), new ChatRequest("Q?"), CancellationToken.None))
        {
            events.Add(evt);
        }

        Assert.IsType<ChatStreamEvent.Meta>(events[0]);
        var token = Assert.IsType<ChatStreamEvent.Token>(events[1]);
        Assert.Equal("Hi", token.Text);
        Assert.IsType<ChatStreamEvent.Error>(events[^1]);
        Assert.DoesNotContain(events, e => e is ChatStreamEvent.Done);
    }
}
