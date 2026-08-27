namespace ProductivityHub.Services
{
    public class TaskEmbeddingBackgroundService : BackgroundService
    {
        private readonly ITaskEmbeddingQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TaskEmbeddingBackgroundService> _logger;

        public TaskEmbeddingBackgroundService(
            ITaskEmbeddingQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<TaskEmbeddingBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var reconciler = scope.ServiceProvider.GetRequiredService<StrandedTaskReconciler>();
                await reconciler.RequeueStrandedTasksAsync(stoppingToken);
            }

            await foreach (var taskId in _queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<TaskEmbeddingProcessor>();
                    await processor.ProcessAsync(taskId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // app is shutting down
                }
                catch (Exception ex)
                {
                    // One bad task must not kill the loop.
                    _logger.LogError(ex, "Unexpected error while processing embedding queue item for task {TaskId}.", taskId);
                }
            }
        }
    }
}
