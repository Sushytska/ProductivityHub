using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ProductivityHub.Services
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;
        private readonly ILogger<OllamaEmbeddingService> _logger;

        public OllamaEmbeddingService(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            if (texts.Count == 0)
            {
                return Array.Empty<float[]>();
            }

            var requestBody = new EmbedRequest(_options.EmbeddingModel, texts.ToArray());

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync("/api/embed", requestBody, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to reach Ollama at {BaseUrl} while generating embeddings.", _httpClient.BaseAddress);
                throw new EmbeddingGenerationException("Could not reach the Ollama embedding service.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Ollama embedding request failed with status {StatusCode}: {Body}", response.StatusCode, body);
                throw new EmbeddingGenerationException($"Ollama returned {(int)response.StatusCode} while generating embeddings.");
            }

            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: cancellationToken);

            if (result?.Embeddings == null || result.Embeddings.Length != texts.Count)
            {
                _logger.LogError(
                    "Ollama returned an unexpected embeddings payload (expected {Expected} vectors, got {Actual}).",
                    texts.Count, result?.Embeddings?.Length ?? 0);
                throw new EmbeddingGenerationException("Ollama returned an unexpected number of embeddings.");
            }

            return result.Embeddings;
        }

        private sealed record EmbedRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("input")] string[] Input);

        private sealed record EmbedResponse(
            [property: JsonPropertyName("model")] string? Model,
            [property: JsonPropertyName("embeddings")] float[][]? Embeddings);
    }
}
