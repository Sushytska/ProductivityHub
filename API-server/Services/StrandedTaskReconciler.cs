using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    /// <summary>
    /// Re-queues tasks left in Pending/Processing state by a worker crash, a shutdown that
    /// raced the retry delay, or a failed re-enqueue after a transient Redis error. Runs once
    /// at startup, before the background worker starts draining the live queue. Also what
    /// picks up any task that predates the embedding pipeline shipping (migrated to Pending
    /// by default), since those are otherwise never enqueued on their own.
    /// </summary>
    public class StrandedTaskReconciler
    {
        private readonly AppDbContext _db;
        private readonly ITaskEmbeddingQueue _queue;
        private readonly ILogger<StrandedTaskReconciler> _logger;

        public StrandedTaskReconciler(AppDbContext db, ITaskEmbeddingQueue queue, ILogger<StrandedTaskReconciler> logger)
        {
            _db = db;
            _queue = queue;
            _logger = logger;
        }

        public async Task RequeueStrandedTasksAsync(CancellationToken cancellationToken = default)
        {
            var strandedTaskIds = await _db.Tasks
                .Where(t => t.EmbeddingStatus == EmbeddingStatus.Pending || t.EmbeddingStatus == EmbeddingStatus.Processing)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            foreach (var taskId in strandedTaskIds)
            {
                _queue.Enqueue(taskId);
            }

            if (strandedTaskIds.Count > 0)
            {
                _logger.LogInformation(
                    "Re-queued {Count} task(s) left in Pending/Processing state from a previous run.",
                    strandedTaskIds.Count);
            }
        }
    }
}
