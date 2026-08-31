namespace ProductivityHub.Services
{
    public interface IChatService
    {
        Task<string> GetAnswerAsync(
            string question, IReadOnlyList<RagSourceItem> contextItems, CancellationToken cancellationToken = default);

        IAsyncEnumerable<string> StreamAnswerAsync(
            string question, IReadOnlyList<RagSourceItem> contextItems,
            CancellationToken cancellationToken = default);
    }
}
