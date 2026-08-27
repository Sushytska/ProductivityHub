using System.Runtime.CompilerServices;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

internal class FakeChatService : IChatService
{
    public string Answer { get; set; } = "fake answer";
    public IReadOnlyList<string>? StreamTokens { get; set; }

    public Task<string> GetAnswerAsync(
        string question, IReadOnlyList<RagSourceItem> contextItems, CancellationToken cancellationToken = default) =>
        Task.FromResult(Answer);

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        string question, IReadOnlyList<RagSourceItem> contextItems,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var token in StreamTokens ?? new[] { Answer })
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
        }

        await Task.CompletedTask;
    }
}
