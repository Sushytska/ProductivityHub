using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace ProductivityHub.Services
{
    public class NoteEmbeddingQueue : INoteEmbeddingQueue
    {
        private const string QueueKey = "note-embedding-queue";
        private const string BlockingTimeoutSeconds = "5";

        private readonly IConnectionMultiplexer _redis;

        public NoteEmbeddingQueue(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public void Enqueue(Guid noteId)
        {
            var db = _redis.GetDatabase();
            db.ListRightPush(QueueKey, noteId.ToString());
        }

        public async IAsyncEnumerable<Guid> DequeueAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var db = _redis.GetDatabase();

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

                if (Guid.TryParse(values[1].ToString(), out var noteId))
                {
                    yield return noteId;
                }
            }
        }
    }
}
