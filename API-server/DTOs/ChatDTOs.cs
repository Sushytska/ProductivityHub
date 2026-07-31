using System.ComponentModel.DataAnnotations;

namespace ProductivityHub.DTOs
{
    public class ChatDTOs
    {
        public record ChatRequest([Required, MinLength(1)] string Question);

        public record ChatResponse(string Answer, DateTime CreatedDate, IReadOnlyList<ChatSourceResponse> Sources);

        public record ChatSourceResponse(Guid NoteId, string NoteTitle, int ChunkIndex);
    }
}
