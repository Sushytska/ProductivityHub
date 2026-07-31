using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Ask(ChatRequest request, CancellationToken cancellationToken)
        {
            var response = await _chatOrchestrationService.AskAsync(GetUserId(), request, cancellationToken);
            return Ok(response);
        }

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
