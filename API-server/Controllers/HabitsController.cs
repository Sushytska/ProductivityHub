using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductivityHub.Services;
using System.Security.Claims;
using static ProductivityHub.DTOs.HabitDTOs;

namespace ProductivityHub.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class HabitsController : ControllerBase
    {
        private readonly HabitService _habitService;

        public HabitsController(HabitService habitService)
        {
            _habitService = habitService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateHabitRequest request)
        {
            var habit = await _habitService.CreateAsync(GetUserId(), request);
            return CreatedAtAction(nameof(GetById), new { id = habit.Id }, habit);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateOnly? asOfDate)
        {
            return Ok(await _habitService.GetAllAsync(GetUserId(), asOfDate));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id, [FromQuery] DateOnly? asOfDate)
        {
            var habit = await _habitService.GetByIdAsync(GetUserId(), id, asOfDate);
            return habit == null ? NotFound() : Ok(habit);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateHabitRequest request)
        {
            var habit = await _habitService.UpdateAsync(GetUserId(), id, request);
            return habit == null ? NotFound() : Ok(habit);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _habitService.DeleteAsync(GetUserId(), id);
            return deleted ? NoContent() : NotFound();
        }

        [HttpPost("{id}/toggle")]
        public async Task<IActionResult> ToggleCompletion(Guid id, ToggleCompletionRequest request)
        {
            var habit = await _habitService.ToggleCompletionAsync(GetUserId(), id, request.Date, request.AsOfDate);
            return habit == null ? NotFound() : Ok(habit);
        }

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
