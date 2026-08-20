using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductivityHub.Services;
using System.Security.Claims;
using System.Text.Json;
using static ProductivityHub.DTOs.ChatDTOs;

namespace ProductivityHub.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatOrchestrationService _chatOrchestrationService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ChatOrchestrationService chatOrchestrationService, ILogger<ChatController> logger)
        {
            _chatOrchestrationService = chatOrchestrationService;
            _logger = logger;
        }

        [HttpPost]
        [EnableRateLimiting("chat")]
        public async Task<IActionResult> Ask(ChatRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _chatOrchestrationService.AskAsync(GetUserId(), request, cancellationToken);
                return Ok(response);
            }
            catch (Exception ex) when (ex is ChatGenerationException or EmbeddingGenerationException)
            {
                return Problem(
                    detail: "The AI service is currently unavailable. Please try again shortly.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }

        [HttpPost("stream")]
        [EnableRateLimiting("chat")]
        public async Task StreamAsk(ChatRequest request, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            try
            {
                await foreach (var evt in _chatOrchestrationService.AskStreamingAsync(GetUserId(), request, cancellationToken))
                {
                    switch (evt)
                    {
                        case ChatStreamEvent.Meta m:
                            await WriteEventAsync("meta", new ChatStreamMetaEvent(m.Sources), cancellationToken);
                            break;
                        case ChatStreamEvent.Token t:
                            await WriteEventAsync("token", new ChatStreamTokenEvent(t.Text), cancellationToken);
                            break;
                        case ChatStreamEvent.Error e:
                            await WriteEventAsync("error", new ChatStreamErrorEvent(e.Message), cancellationToken);
                            break;
                        case ChatStreamEvent.Done:
                            await WriteEventAsync("done", new { }, cancellationToken);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client disconnected — nothing left to write.
            }
            catch (Exception ex) when (ex is ChatGenerationException or EmbeddingGenerationException)
            {
                await TryWriteErrorEventAsync(
                    "The AI service is currently unavailable. Please try again shortly.", cancellationToken);
            }
            catch (Exception ex)
            {
                // A streaming response may already have flushed SSE frames, so this can't be
                // turned into a normal 5xx status — best-effort notify the client instead of
                // letting the connection truncate silently with only server-side logging.
                _logger.LogError(ex, "Unhandled error while streaming a chat answer.");
                await TryWriteErrorEventAsync("An unexpected error occurred. Please try again shortly.", cancellationToken);
            }
        }

        private async Task TryWriteErrorEventAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                await WriteEventAsync("error", new ChatStreamErrorEvent(message), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write an SSE error frame after streaming failure.");
            }
        }

        // Matches ASP.NET Core MVC's own JSON formatter default (camelCase) — Ok(response)
        // results elsewhere in this API already serialize that way, and the Node relay/
        // Socket.IO clients expect the same casing (e.g. payload.text, not payload.Text).
        private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

        private async Task WriteEventAsync(string eventName, object payload, CancellationToken cancellationToken)
        {
            await Response.WriteAsync($"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, SseJsonOptions)}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
