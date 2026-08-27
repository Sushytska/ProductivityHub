using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ProductivityHub.Models;

namespace ProductivityHub.Services
{
    public class AnthropicChatService : IChatService
    {
        public const string NoContextAnswer =
            "I don't have any notes or tasks that seem relevant to that question yet.";

        private const string SystemPrompt =
            "You are a personal assistant answering questions using only the user's own notes and tasks provided " +
            "in this conversation. Ground your answer in the provided notes and tasks. If they don't contain the " +
            "answer, say so honestly rather than guessing.";

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
            string question, IReadOnlyList<RagSourceItem> contextItems, CancellationToken cancellationToken = default)
        {
            if (contextItems.Count == 0)
            {
                _logger.LogInformation("No relevant notes or tasks found; returning canned no-context response.");
                return NoContextAnswer;
            }

            var requestBody = new MessageRequest(
                _options.Model,
                _options.MaxTokens,
                SystemPrompt,
                new[] { new MessageParam(ChatRoles.User, BuildUserContent(question, contextItems)) });

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

        public async IAsyncEnumerable<string> StreamAnswerAsync(
            string question, IReadOnlyList<RagSourceItem> contextItems,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (contextItems.Count == 0)
            {
                _logger.LogInformation("No relevant notes or tasks found; streaming canned no-context response as a single token.");
                yield return NoContextAnswer;
                yield break;
            }

            var requestBody = new MessageRequest(
                _options.Model,
                _options.MaxTokens,
                SystemPrompt,
                new[] { new MessageParam(ChatRoles.User, BuildUserContent(question, contextItems)) },
                Stream: true);

            // PostAsJsonAsync buffers the whole response body before returning, which would
            // defeat streaming entirely — SendAsync + ResponseHeadersRead returns as soon as
            // headers arrive so the SSE body can be read incrementally as bytes arrive.
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
            {
                Content = JsonContent.Create(requestBody)
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to reach the Anthropic API while streaming a chat answer.");
                throw new ChatGenerationException("Could not reach the Anthropic API.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Anthropic API call timed out while streaming a chat answer.");
                throw new ChatGenerationException("The Anthropic API did not respond in time.", ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Anthropic streaming request failed with status {StatusCode}: {Body}", response.StatusCode, body);
                    throw new ChatGenerationException($"Anthropic returned {(int)response.StatusCode} while streaming a chat answer.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                // Manual enumeration (not `await foreach`) so MoveNextAsync/Deserialize can be
                // wrapped in a try/catch — `yield return` is illegal inside a try block that has
                // a catch, so the yield happens after this block, never inside it (see below).
                var frames = SseFrameParser.ExtractDataPayloads(ReadLinesAsync(reader, cancellationToken));
                await using var enumerator = frames.GetAsyncEnumerator(cancellationToken);

                // Anthropic always terminates a successful stream with a "message_stop" frame.
                // The underlying HTTP body can end (MoveNextAsync -> false) without one ever
                // arriving — e.g. an idle-timeout or proxy closing the connection mid-response —
                // in which case the answer collected so far is truncated, not complete, and must
                // not be treated (and persisted) as a normal finish.
                var sawMessageStop = false;

                while (true)
                {
                    StreamEnvelope? envelope;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                        {
                            break;
                        }

                        envelope = JsonSerializer.Deserialize<StreamEnvelope>(enumerator.Current);
                    }
                    catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Same distinction as the initial SendAsync catch: HttpClient.Timeout
                        // also bounds reading the streamed body, not just the request headers.
                        _logger.LogError(ex, "Anthropic API call timed out while reading a streamed chat answer.");
                        throw new ChatGenerationException("The Anthropic API did not respond in time.", ex);
                    }
                    catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException)
                    {
                        _logger.LogError(ex, "Lost connection to the Anthropic API while streaming a chat answer.");
                        throw new ChatGenerationException("Lost connection to the Anthropic API while streaming.", ex);
                    }

                    if (envelope?.Type == "content_block_delta" && envelope.Delta?.Type == "text_delta"
                        && !string.IsNullOrEmpty(envelope.Delta.Text))
                    {
                        yield return envelope.Delta.Text;
                    }
                    else if (envelope?.Type == "message_stop")
                    {
                        sawMessageStop = true;
                    }
                    else if (envelope?.Type == "error")
                    {
                        throw new ChatGenerationException($"Anthropic streaming error: {envelope.Error?.Message ?? "unknown"}");
                    }
                }

                if (!sawMessageStop)
                {
                    _logger.LogError("Anthropic stream ended without a message_stop frame; treating as a truncated response.");
                    throw new ChatGenerationException("The Anthropic stream ended before completion.");
                }
            }
        }

        private static async IAsyncEnumerable<string> ReadLinesAsync(
            StreamReader reader, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                yield return line;
            }
        }

        public static string BuildUserContent(string question, IReadOnlyList<RagSourceItem> contextItems)
        {
            var contextBlock = string.Join("\n\n", contextItems.Select((item, i) =>
                $"[{item.SourceType} {i + 1} - \"{item.Title}\"]\n{item.Text}"));

            return $"{contextBlock}\n\nQuestion: {question}";
        }

        private sealed record MessageRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("max_tokens")] int MaxTokens,
            [property: JsonPropertyName("system")] string System,
            [property: JsonPropertyName("messages")] MessageParam[] Messages,
            [property: JsonPropertyName("stream")] bool Stream = false);

        private sealed record MessageParam(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content);

        private sealed record MessageResponse(
            [property: JsonPropertyName("content")] ContentBlock[]? Content);

        private sealed record ContentBlock(
            [property: JsonPropertyName("type")] string Type,
            [property: JsonPropertyName("text")] string? Text);

        private sealed record StreamEnvelope(
            [property: JsonPropertyName("type")] string? Type,
            [property: JsonPropertyName("delta")] StreamDelta? Delta,
            [property: JsonPropertyName("error")] StreamError? Error);

        private sealed record StreamDelta(
            [property: JsonPropertyName("type")] string? Type,
            [property: JsonPropertyName("text")] string? Text);

        private sealed record StreamError(
            [property: JsonPropertyName("message")] string? Message);
    }
}
