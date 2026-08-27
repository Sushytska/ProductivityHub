using Microsoft.EntityFrameworkCore;
using Pgvector;
using ProductivityHub.Database;
using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    public class TaskEmbeddingProcessor
    {
        public const int MaxEmbeddingAttempts = 3;
        public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);

        private readonly AppDbContext _db;
        private readonly IEmbeddingService _embeddingService;
        private readonly ITaskEmbeddingQueue _queue;
        private readonly ILogger<TaskEmbeddingProcessor> _logger;
        private readonly TimeSpan _retryDelay;

        public TaskEmbeddingProcessor(
            AppDbContext db,
            IEmbeddingService embeddingService,
            ITaskEmbeddingQueue queue,
            ILogger<TaskEmbeddingProcessor> logger,
            TimeSpan? retryDelay = null)
        {
            _db = db;
            _embeddingService = embeddingService;
            _queue = queue;
            _logger = logger;
            _retryDelay = retryDelay ?? DefaultRetryDelay;
        }

        public async Task ProcessAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
            if (task == null)
            {
                // Task was deleted (or the id was stale) before we got to it — skip silently.
                return;
            }

            task.EmbeddingStatus = EmbeddingStatus.Processing;
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                // Same text is used here (embedded) and by RagService (shown to Claude) via
                // BuildContextText() — title carries most of the semantic weight for retrieval
                // and isn't repeated in the context block, since ChatOrchestrationService's
                // "[Task N - title]" header already shows it.
                var embeddingInput = $"{task.Title}\n{task.BuildContextText()}";
                var vectors = await _embeddingService.GenerateEmbeddingsAsync(new[] { embeddingInput }, cancellationToken);

                task.Embedding = new Vector(vectors[0]);
                task.EmbeddingStatus = EmbeddingStatus.Completed;
                task.EmbeddingAttempts = 0;
                task.EmbeddingError = null;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                task.EmbeddingAttempts++;
                task.EmbeddingError = ex.Message;

                if (task.EmbeddingAttempts < MaxEmbeddingAttempts)
                {
                    task.EmbeddingStatus = EmbeddingStatus.Pending;
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogWarning(ex, "Embedding attempt {Attempt}/{Max} failed for task {TaskId}; retrying after delay.",
                        task.EmbeddingAttempts, MaxEmbeddingAttempts, taskId);

                    // NOTE: this delay is awaited inline in the single-worker queue loop
                    // (TaskEmbeddingBackgroundService), so a failing task blocks every other
                    // queued task from being processed for up to _retryDelay. Acceptable for a
                    // single-user local deployment — same tradeoff NoteEmbeddingProcessor makes.
                    await Task.Delay(_retryDelay, cancellationToken);

                    // The task's status is already saved as Pending above, so if Enqueue itself
                    // fails (e.g. Redis is down), the task isn't lost — StrandedTaskReconciler
                    // will pick it up on the next app startup.
                    try
                    {
                        _queue.Enqueue(taskId);
                    }
                    catch (Exception enqueueEx)
                    {
                        _logger.LogWarning(enqueueEx, "Failed to re-enqueue task {TaskId} after a failed embedding attempt; it will be picked up on the next reconciliation pass.", taskId);
                    }
                }
                else
                {
                    task.EmbeddingStatus = EmbeddingStatus.Failed;
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogError(ex, "Embedding generation permanently failed for task {TaskId} after {Attempts} attempts.",
                        taskId, task.EmbeddingAttempts);
                }
            }
        }
    }
}
