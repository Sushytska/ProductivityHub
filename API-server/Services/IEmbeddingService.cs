namespace ProductivityHub.Services
{
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generates one embedding vector per input text, in a single batched call.
        /// The returned list is index-aligned with <paramref name="texts"/> (result[i] is the embedding for texts[i]).
        /// </summary>
        Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default);
    }
}
