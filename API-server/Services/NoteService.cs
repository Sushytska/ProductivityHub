using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;
using static ProductivityHub.DTOs.NoteDTOs;

namespace ProductivityHub.Services
{
    public class NoteService
    {
        private readonly AppDbContext _db;
        private readonly INoteEmbeddingQueue _embeddingQueue;
        private readonly ILogger<NoteService> _logger;

        public NoteService(AppDbContext db, INoteEmbeddingQueue embeddingQueue, ILogger<NoteService> logger)
        {
            _db = db;
            _embeddingQueue = embeddingQueue;
            _logger = logger;
        }

        public async Task<NoteResponse> CreateAsync(Guid userId, CreateNoteRequest request)
        {
            var note = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = request.Title,
                Content = request.Content,
                CreatedDate = DateTime.UtcNow
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            TryEnqueueEmbedding(note.Id);

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
            note.EmbeddingStatus = EmbeddingStatus.Pending;
            note.EmbeddingAttempts = 0;
            note.EmbeddingError = null;

            await _db.SaveChangesAsync();

            TryEnqueueEmbedding(note.Id);

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

        private void TryEnqueueEmbedding(Guid noteId)
        {
            // The note is already committed at this point (EmbeddingStatus=Pending), so a queue
            // failure here must not fail the request. StrandedNoteReconciler picks up any note
            // left in Pending with no queue entry the next time the app starts.
            try
            {
                _embeddingQueue.Enqueue(noteId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue note {NoteId} for embedding; it will be picked up on the next reconciliation pass.", noteId);
            }
        }

        private static NoteResponse ToResponse(Note note) =>
            new(note.Id, note.Title, note.Content, note.CreatedDate);
    }
}
