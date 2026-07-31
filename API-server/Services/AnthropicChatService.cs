using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    public class AnthropicChatService : IChatService
    {
        public const string NoContextAnswer =
            "I don't have any notes that seem relevant to that question yet.";

        private const string SystemPrompt =
            "You are a personal assistant answering questions using only the user's own notes provided in this " +
            "conversation. Ground your answer in the provided notes. If the notes don't contain the answer, say so " +
            "honestly rather than guessing.";

        private readonly HttpClient _httpClient;
        private readonly AnthropicOptions _options;
        private readonly ILogger<AnthropicChatService> _logger;

        public AnthropicChatService(HttpClient httpClient, IOptions<AnthropicOptions> options, ILogger<AnthropicChatService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<string> GetAnswerAsync(
            string question, IReadOnlyList<NoteChunk> contextChunks, CancellationToken cancellationToken = default)
        {
            if (contextChunks.Count == 0)
            {
                _logger.LogInformation("No relevant note chunks found; returning canned no-context response.");
                return NoContextAnswer;
            }

            var requestBody = new MessageRequest(
                _options.Model,
                _options.MaxTokens,
                SystemPrompt,
                new[] { new MessageParam(ChatRoles.User, BuildUserContent(question, contextChunks)) });

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync("/v1/messages", requestBody, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to reach the Anthropic API while answering a chat question.");
                throw new ChatGenerationException("Could not reach the Anthropic API.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient.Timeout elapsed — this is distinct from the caller cancelling
                // cancellationToken itself, which we let propagate as a normal cancellation.
                _logger.LogError(ex, "Anthropic API call timed out while answering a chat question.");
                throw new ChatGenerationException("The Anthropic API did not respond in time.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Anthropic chat request failed with status {StatusCode}: {Body}", response.StatusCode, body);
                throw new ChatGenerationException($"Anthropic returned {(int)response.StatusCode} while answering a chat question.");
            }

            var result = await response.Content.ReadFromJsonAsync<MessageResponse>(cancellationToken: cancellationToken);
            var answer = result?.Content?.FirstOrDefault(b => b.Type == "text")?.Text;

            if (string.IsNullOrWhiteSpace(answer))
            {
                _logger.LogError("Anthropic returned no text content for a chat question.");
                throw new ChatGenerationException("Anthropic returned an empty response.");
            }

            return answer;
        }

        public static string BuildUserContent(string question, IReadOnlyList<NoteChunk> contextChunks)
        {
            var contextBlock = string.Join("\n\n", contextChunks.Select((c, i) =>
                $"[Note {i + 1} - \"{c.Note.Title}\"]\n{c.ChunkText}"));

            return $"{contextBlock}\n\nQuestion: {question}";
        }

        private sealed record MessageRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("max_tokens")] int MaxTokens,
            [property: JsonPropertyName("system")] string System,
            [property: JsonPropertyName("messages")] MessageParam[] Messages);

        private sealed record MessageParam(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content);

        private sealed record MessageResponse(
            [property: JsonPropertyName("content")] ContentBlock[]? Content);

        private sealed record ContentBlock(
            [property: JsonPropertyName("type")] string Type,
            [property: JsonPropertyName("text")] string? Text);
    }
}
