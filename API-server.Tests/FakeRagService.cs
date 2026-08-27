using ProductivityHub.Services;

namespace ProductivityHub.Tests;

internal class FakeRagService : IRagService
{
    private readonly Func<Guid, string, int, IReadOnlyList<RagSourceItem>> _getChunks;

    public FakeRagService(Func<Guid, string, int, IReadOnlyList<RagSourceItem>> getChunks)
    {
        _getChunks = getChunks;
    }

    public Task<IReadOnlyList<RagSourceItem>> GetRelevantChunksAsync(
        Guid userId, string query, int topK = 5, CancellationToken cancellationToken = default) =>
        Task.FromResult(_getChunks(userId, query, topK));
}
