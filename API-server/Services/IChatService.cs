using System.Runtime.CompilerServices;
using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    public interface IChatService
    {
        Task<string> GetAnswerAsync(
            string question, IReadOnlyList<NoteChunk> contextChunks, CancellationToken cancellationToken = default);

        IAsyncEnumerable<string> StreamAnswerAsync(
            string question, IReadOnlyList<NoteChunk> contextChunks,
            [EnumeratorCancellation] CancellationToken cancellationToken = default);
    }
}
