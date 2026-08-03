namespace ProductivityHub.Services
{
    public class ChatGenerationException : Exception
    {
        public ChatGenerationException(string message) : base(message)
        {
        }

        public ChatGenerationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
