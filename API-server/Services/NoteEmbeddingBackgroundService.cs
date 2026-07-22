namespace ProductivityHub.Services
{
    public class NoteEmbeddingBackgroundService : BackgroundService
    {
        private readonly INoteEmbeddingQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NoteEmbeddingBackgroundService> _logger;

        public NoteEmbeddingBackgroundService(
            INoteEmbeddingQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<NoteEmbeddingBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var reconciler = scope.ServiceProvider.GetRequiredService<StrandedNoteReconciler>();
                await reconciler.RequeueStrandedNotesAsync(stoppingToken);
            }

            await foreach (var noteId in _queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<NoteEmbeddingProcessor>();
                    await processor.ProcessAsync(noteId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // app is shutting down
                }
                catch (Exception ex)
                {
                    // One bad note must not kill the loop.
                    _logger.LogError(ex, "Unexpected error while processing embedding queue item for note {NoteId}.", noteId);
                }
            }
        }
    }
}
