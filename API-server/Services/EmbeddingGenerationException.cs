namespace ProductivityHub.Services
{
    public class EmbeddingGenerationException : Exception
    {
        public EmbeddingGenerationException(string message) : base(message)
        {
        }

        public EmbeddingGenerationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
