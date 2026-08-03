using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductivityHub.Services;
using System.Security.Claims;
using static ProductivityHub.DTOs.ChatDTOs;

namespace ProductivityHub.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatOrchestrationService _chatOrchestrationService;

        public ChatController(ChatOrchestrationService chatOrchestrationService)
        {
            _chatOrchestrationService = chatOrchestrationService;
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

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
