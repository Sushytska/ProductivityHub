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

                // Delete without loading: select only the Id (never the embedding vector) and
                // remove via key-only stub entities, so re-chunking a note never pulls its old
                // vectors over the wire just to discard them. If a chunk happens to already be
                // tracked (e.g. an eager-loaded Note.Chunks earlier in the same scope), reuse
                // that tracked instance instead of attaching a conflicting stub with the same key.
                var existingChunkIds = await _db.NoteChunks
                    .Where(c => c.NoteId == noteId)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);

                foreach (var chunkId in existingChunkIds)
                {
                    var tracked = _db.ChangeTracker.Entries<NoteChunk>()
                        .FirstOrDefault(e => e.Entity.Id == chunkId)?.Entity;

                    _db.NoteChunks.Remove(tracked ?? new NoteChunk { Id = chunkId, ChunkText = string.Empty });
                }

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

                    // NOTE: this delay is awaited inline in the single-worker queue loop
                    // (NoteEmbeddingBackgroundService), so a failing note blocks every other
                    // queued note from being processed for up to _retryDelay. Acceptable for a
                    // single-user local deployment; a scheduled/delayed re-enqueue (rather than
                    // sleeping in-band) would be needed to avoid head-of-line blocking at scale.
                    await Task.Delay(_retryDelay, cancellationToken);

                    // The note's status is already saved as Pending above, so if Enqueue itself
                    // fails (e.g. Redis is down), the note isn't lost — StrandedNoteReconciler
                    // will pick it up on the next app startup.
                    try
                    {
                        _queue.Enqueue(noteId);
                    }
                    catch (Exception enqueueEx)
                    {
                        _logger.LogWarning(enqueueEx, "Failed to re-enqueue note {NoteId} after a failed embedding attempt; it will be picked up on the next reconciliation pass.", noteId);
                    }
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
