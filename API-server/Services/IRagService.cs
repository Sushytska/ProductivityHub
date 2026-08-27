namespace ProductivityHub.Services
{
    public interface IRagService
    {
        /// <summary>
        /// Returns up to <paramref name="topK"/> of the calling user's own Note chunks and
        /// Tasks combined, ranked by semantic similarity to <paramref name="query"/> regardless
        /// of source, most-relevant first. Never returns items belonging to another user.
        /// </summary>
        Task<IReadOnlyList<RagSourceItem>> GetRelevantChunksAsync(
            Guid userId, string query, int topK = 5, CancellationToken cancellationToken = default);
    }
}
