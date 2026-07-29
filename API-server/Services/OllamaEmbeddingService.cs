using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ProductivityHub.Services
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        // Must match the NoteChunk.Embedding column type (vector(768) — see AppDbContext).
        private const int ExpectedEmbeddingDimension = 768;

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

            for (var i = 0; i < result.Embeddings.Length; i++)
            {
                var vector = result.Embeddings[i];
                if (vector == null || vector.Length != ExpectedEmbeddingDimension)
                {
                    _logger.LogError(
                        "Ollama returned an embedding with an unexpected shape for chunk {Index}: expected {Expected} dimensions, got {Actual}.",
                        i, ExpectedEmbeddingDimension, vector?.Length.ToString() ?? "null");
                    throw new EmbeddingGenerationException(
                        $"Ollama returned {(vector == null ? "no vector" : $"a {vector.Length}-dimensional vector")} for chunk {i}, expected {ExpectedEmbeddingDimension} dimensions. " +
                        $"Check that the configured Ollama:EmbeddingModel ('{_options.EmbeddingModel}') produces {ExpectedEmbeddingDimension}-dimensional embeddings.");
                }
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
