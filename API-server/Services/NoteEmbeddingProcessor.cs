using Microsoft.EntityFrameworkCore;
using Pgvector;
using ProductivityHub.Database;
using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    public class NoteEmbeddingProcessor
    {
        public const int MaxEmbeddingAttempts = 3;
        public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);

        private readonly AppDbContext _db;
        private readonly INoteChunker _chunker;
        private readonly IEmbeddingService _embeddingService;
        private readonly INoteEmbeddingQueue _queue;
        private readonly ILogger<NoteEmbeddingProcessor> _logger;
        private readonly TimeSpan _retryDelay;

        public NoteEmbeddingProcessor(
            AppDbContext db,
            INoteChunker chunker,
            IEmbeddingService embeddingService,
            INoteEmbeddingQueue queue,
            ILogger<NoteEmbeddingProcessor> logger,
            TimeSpan? retryDelay = null)
        {
            _db = db;
            _chunker = chunker;
            _embeddingService = embeddingService;
            _queue = queue;
            _logger = logger;
            _retryDelay = retryDelay ?? DefaultRetryDelay;
        }

        public async Task ProcessAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken);
            if (note == null)
            {
                // Note was deleted (or the id was stale) before we got to it — skip silently.
                return;
            }

            note.EmbeddingStatus = EmbeddingStatus.Processing;
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                var chunkTexts = _chunker.Chunk(note.Content);

                var existingChunks = await _db.NoteChunks
                    .Where(c => c.NoteId == noteId)
                    .ToListAsync(cancellationToken);
                _db.NoteChunks.RemoveRange(existingChunks);

                if (chunkTexts.Count > 0)
                {
                    var vectors = await _embeddingService.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);

                    var newChunks = chunkTexts.Select((text, i) => new NoteChunk
                    {
                        Id = Guid.NewGuid(),
                        NoteId = note.Id,
                        ChunkText = text,
                        ChunkIndex = i,
                        Embedding = new Vector(vectors[i])
                    });

                    _db.NoteChunks.AddRange(newChunks);
                }

                note.EmbeddingStatus = EmbeddingStatus.Completed;
                note.EmbeddingAttempts = 0;
                note.EmbeddingError = null;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                note.EmbeddingAttempts++;
                note.EmbeddingError = ex.Message;

                if (note.EmbeddingAttempts < MaxEmbeddingAttempts)
                {
                    note.EmbeddingStatus = EmbeddingStatus.Pending;
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogWarning(ex, "Embedding attempt {Attempt}/{Max} failed for note {NoteId}; retrying after delay.",
                        note.EmbeddingAttempts, MaxEmbeddingAttempts, noteId);

                    await Task.Delay(_retryDelay, cancellationToken);
                    _queue.Enqueue(noteId);
                }
                else
                {
                    note.EmbeddingStatus = EmbeddingStatus.Failed;
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogError(ex, "Embedding generation permanently failed for note {NoteId} after {Attempts} attempts.",
                        noteId, note.EmbeddingAttempts);
                }
            }
        }
    }
}
