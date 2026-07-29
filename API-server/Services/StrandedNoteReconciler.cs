using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    /// <summary>
    /// Re-queues notes left in Pending/Processing state by a worker crash, a shutdown that
    /// raced the retry delay, or a failed re-enqueue after a transient Redis error. Runs once
    /// at startup, before the background worker starts draining the live queue.
    /// </summary>
    public class StrandedNoteReconciler
    {
        private readonly AppDbContext _db;
        private readonly INoteEmbeddingQueue _queue;
        private readonly ILogger<StrandedNoteReconciler> _logger;

        public StrandedNoteReconciler(AppDbContext db, INoteEmbeddingQueue queue, ILogger<StrandedNoteReconciler> logger)
        {
            _db = db;
            _queue = queue;
            _logger = logger;
        }

        public async Task RequeueStrandedNotesAsync(CancellationToken cancellationToken = default)
        {
            var strandedNoteIds = await _db.Notes
                .Where(n => n.EmbeddingStatus == EmbeddingStatus.Pending || n.EmbeddingStatus == EmbeddingStatus.Processing)
                .Select(n => n.Id)
                .ToListAsync(cancellationToken);

            foreach (var noteId in strandedNoteIds)
            {
                _queue.Enqueue(noteId);
            }

            if (strandedNoteIds.Count > 0)
            {
                _logger.LogInformation(
                    "Re-queued {Count} note(s) left in Pending/Processing state from a previous run.",
                    strandedNoteIds.Count);
            }
        }
    }
}
