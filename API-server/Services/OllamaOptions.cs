namespace ProductivityHub.Services
{
    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";

        public string EmbeddingModel { get; set; } = "nomic-embed-text";
    }
}
