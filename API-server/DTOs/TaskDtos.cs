using System.ComponentModel.DataAnnotations;

namespace ProductivityHub.DTOs
{
    public class TaskDTOs
    {
        // MaxLength bounds what TaskEmbeddingProcessor sends to Ollama as a single,
        // unchunked embedding input (unlike Notes, which NoteChunker splits into
        // 500-word chunks) — this is also the same text RagService later shows Claude
        // as context, so keeping it bounded here (rather than truncating later, which
        // would make the embedded text and the displayed context text diverge) is what
        // keeps both in sync.
        public record CreateTaskRequest([MaxLength(200)] string Title, [MaxLength(2000)] string? Description, bool IsCompleted, DateOnly? DueDate);

        public record UpdateTaskRequest([MaxLength(200)] string Title, [MaxLength(2000)] string? Description, bool IsCompleted, DateOnly? DueDate);

        public record TaskResponse(Guid Id, string Title, string? Description, bool IsCompleted, DateOnly? DueDate, DateTime CreatedDate);
    }
}
