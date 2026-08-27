using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using ProductivityHub.Database;

namespace ProductivityHub.Services
{
    public class RagService : IRagService
    {
        private readonly AppDbContext _db;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<RagService> _logger;

        public RagService(AppDbContext db, IEmbeddingService embeddingService, ILogger<RagService> logger)
        {
            _db = db;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<RagSourceItem>> GetRelevantChunksAsync(
            Guid userId, string query, int topK = 5, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogInformation("Empty or whitespace chat query for user {UserId}; skipping retrieval.", userId);
                return Array.Empty<RagSourceItem>();
            }

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { query }, cancellationToken);
            var queryVector = new Vector(embeddings[0]);

            // Fetch up to topK candidates from each source (each already ordered by distance in
            // SQL), then merge by distance in C# and cap to the overall topK — so the context
            // handed to Claude is whichever items are actually most relevant, regardless of
            // whether they came from a note or a task, not a fixed split between the two.
            var noteResults = await _db.NoteChunks
                .Include(c => c.Note)
                .Where(c => c.Note.UserId == userId && c.Embedding != null)
                .Select(c => new { c, Distance = c.Embedding!.CosineDistance(queryVector) })
                .OrderBy(x => x.Distance)
                .Take(topK)
                .ToListAsync(cancellationToken);

            var taskResults = await _db.Tasks
                .Where(t => t.UserId == userId && t.Embedding != null)
                .Select(t => new { t, Distance = t.Embedding!.CosineDistance(queryVector) })
                .OrderBy(x => x.Distance)
                .Take(topK)
                .ToListAsync(cancellationToken);

            return noteResults
                .Select(x => new RagSourceItem("Note", x.c.NoteId, x.c.Note.Title, x.c.ChunkText, x.c.ChunkIndex, x.Distance))
                .Concat(taskResults.Select(x => new RagSourceItem("Task", x.t.Id, x.t.Title, x.t.BuildContextText(), 0, x.Distance)))
                .OrderBy(item => item.Distance)
                .Take(topK)
                .ToList();
        }
    }
}
