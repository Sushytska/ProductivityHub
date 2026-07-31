using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    public interface IRagService
    {
        /// <summary>
        /// Returns up to <paramref name="topK"/> of the calling user's own NoteChunks
        /// most semantically similar to <paramref name="query"/>, ordered most-relevant first.
        /// Never returns chunks belonging to another user.
        /// </summary>
        Task<IReadOnlyList<NoteChunk>> GetRelevantChunksAsync(
            Guid userId, string query, int topK = 5, CancellationToken cancellationToken = default);
    }
}
