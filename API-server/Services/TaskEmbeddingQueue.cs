using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace ProductivityHub.Services
{
    public class TaskEmbeddingQueue : ITaskEmbeddingQueue, IDisposable
    {
        private const string QueueKey = "task-embedding-queue";
        private const string BlockingTimeoutSeconds = "5";

        private readonly IConnectionMultiplexer _redis;
        private readonly ConnectionMultiplexer _blockingRedis;

        public TaskEmbeddingQueue(IConnectionMultiplexer redis, IConfiguration configuration)
        {
            _redis = redis;

            // Dedicated connection for BLPOP only — see NoteEmbeddingQueue for why: sharing one
            // connection between a near-continuous blocking poll loop and ordinary Enqueue
            // (RPUSH) calls serializes the RPUSH behind whichever BLPOP happens to be in flight.
            var options = ConfigurationOptions.Parse(configuration["Redis:ConnectionString"]!);
            options.SyncTimeout = 10000;
            options.AsyncTimeout = 10000;
            _blockingRedis = ConnectionMultiplexer.Connect(options);
        }

        public void Enqueue(Guid taskId)
        {
            var db = _redis.GetDatabase();
            db.ListRightPush(QueueKey, taskId.ToString());
        }

        public async IAsyncEnumerable<Guid> DequeueAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var db = _blockingRedis.GetDatabase();

            while (!cancellationToken.IsCancellationRequested)
            {
                RedisResult result;
                try
                {
                    result = await db.ExecuteAsync("BLPOP", QueueKey, BlockingTimeoutSeconds);
                }
                catch (RedisConnectionException)
                {
                    // Redis briefly unreachable — pause before retrying rather than spinning.
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }
                catch (RedisTimeoutException)
                {
                    // BLPOP's server-side block timeout can race the client's own response
                    // timeout — this just means nothing was queued in time, not a real error.
                    continue;
                }

                if (result.IsNull)
                {
                    continue; // BLPOP timed out with nothing queued — loop again.
                }

                var values = (RedisValue[]?)result;
                if (values == null || values.Length < 2)
                {
                    continue;
                }

                if (Guid.TryParse(values[1].ToString(), out var taskId))
                {
                    yield return taskId;
                }
            }
        }

        public void Dispose() => _blockingRedis.Dispose();
    }
}
