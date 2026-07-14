using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;
using static ProductivityHub.DTOs.NoteDTOs;

namespace ProductivityHub.Services
{
    public class NoteService
    {
        private readonly AppDbContext _db;

        public NoteService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<NoteResponse> CreateAsync(Guid userId, CreateNoteRequest request)
        {
            var note = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = request.Title,
                Content = request.Content,
                Embedding = null,
                CreatedDate = DateTime.UtcNow
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            return ToResponse(note);
        }

        public async Task<List<NoteResponse>> GetAllAsync(Guid userId)
        {
            var notes = await _db.Notes
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .ToListAsync();

            return notes.Select(ToResponse).ToList();
        }

        public async Task<NoteResponse?> GetByIdAsync(Guid userId, Guid noteId)
        {
            var note = await _db.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);

            return note == null ? null : ToResponse(note);
        }

        public async Task<NoteResponse?> UpdateAsync(Guid userId, Guid noteId, UpdateNoteRequest request)
        {
            var note = await _db.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);

            if (note == null)
            {
                return null;
            }

            note.Title = request.Title;
            note.Content = request.Content;

            await _db.SaveChangesAsync();

            return ToResponse(note);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid noteId)
        {
            var note = await _db.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);

            if (note == null)
            {
                return false;
            }

            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();

            return true;
        }

        private static NoteResponse ToResponse(Note note) =>
            new(note.Id, note.Title, note.Content, note.CreatedDate);
    }
}
