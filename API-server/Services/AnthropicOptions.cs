namespace ProductivityHub.Services
{
    public class AnthropicOptions
    {
        public string BaseUrl { get; set; } = "https://api.anthropic.com";

        public string Model { get; set; } = "claude-sonnet-5";

        public string ApiVersion { get; set; } = "2023-06-01";

        public int MaxTokens { get; set; } = 2048;
    }
}
