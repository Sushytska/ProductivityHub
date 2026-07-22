using ProductivityHub.Services;

namespace ProductivityHub.Tests;

internal class FakeEmbeddingService : IEmbeddingService
{
    private readonly Func<IReadOnlyList<string>, IReadOnlyList<float[]>> _generate;

    public FakeEmbeddingService(Func<IReadOnlyList<string>, IReadOnlyList<float[]>> generate)
    {
        _generate = generate;
    }

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_generate(texts));
}
