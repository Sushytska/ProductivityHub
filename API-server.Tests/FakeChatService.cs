using ProductivityHub.Models;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

internal class FakeChatService : IChatService
{
    public string Answer { get; set; } = "fake answer";

    public Task<string> GetAnswerAsync(
        string question, IReadOnlyList<NoteChunk> contextChunks, CancellationToken cancellationToken = default) =>
        Task.FromResult(Answer);
}
