using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductivityHub.Database;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

// NOTE: RagService's actual pgvector similarity queries (CosineDistance, over both NoteChunks
// and Tasks) cannot be exercised here — TestAppDbContext ignores NoteChunk.Embedding and
// TaskItem.Embedding because the EF Core InMemory provider has no pgvector support (see
// TestAppDbContext.cs). This mirrors the existing untested-raw-HTTP-call precedent in
// OllamaEmbeddingService. Only the guard clause is unit-tested below; the real query must be
// verified manually against real Postgres:
//   1. Seed two users, each with a note and a task that have completed embeddings.
//   2. Ask a question relevant to user A's notes/tasks as user A — confirm only user A's
//      items come back, ordered most-relevant first, correctly merged across both sources.
//   3. Confirm at most `topK` items are ever returned in total.
public class RagServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static RagService CreateSut(AppDbContext db, FakeEmbeddingService? embeddingService = null) =>
        new(db, embeddingService ?? new FakeEmbeddingService(_ => throw new InvalidOperationException("IEmbeddingService should not be called.")), NullLogger<RagService>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetRelevantChunksAsync_EmptyOrWhitespaceQuery_ReturnsNoChunksWithoutCallingEmbeddingService(string query)
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.GetRelevantChunksAsync(Guid.NewGuid(), query);

        Assert.Empty(result);
    }
}
