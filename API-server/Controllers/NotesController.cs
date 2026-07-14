using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductivityHub.Services;
using System.Security.Claims;
using static ProductivityHub.DTOs.NoteDTOs;

namespace ProductivityHub.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotesController : ControllerBase
    {
        private readonly NoteService _noteService;

        public NotesController(NoteService noteService)
        {
            _noteService = noteService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateNoteRequest request)
        {
            var note = await _noteService.CreateAsync(GetUserId(), request);
            return CreatedAtAction(nameof(GetById), new { id = note.Id }, note);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _noteService.GetAllAsync(GetUserId()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var note = await _noteService.GetByIdAsync(GetUserId(), id);
            return note == null ? NotFound() : Ok(note);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateNoteRequest request)
        {
            var note = await _noteService.UpdateAsync(GetUserId(), id, request);
            return note == null ? NotFound() : Ok(note);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _noteService.DeleteAsync(GetUserId(), id);
            return deleted ? NoContent() : NotFound();
        }

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
