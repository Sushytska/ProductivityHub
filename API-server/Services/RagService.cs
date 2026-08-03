using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;

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

        public async Task<IReadOnlyList<NoteChunk>> GetRelevantChunksAsync(
            Guid userId, string query, int topK = 5, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogInformation("Empty or whitespace chat query for user {UserId}; skipping retrieval.", userId);
                return Array.Empty<NoteChunk>();
            }

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { query }, cancellationToken);
            var queryVector = new Vector(embeddings[0]);

            return await _db.NoteChunks
                .Include(c => c.Note)
                .Where(c => c.Note.UserId == userId && c.Embedding != null)
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .Take(topK)
                .ToListAsync(cancellationToken);
        }
    }
}
