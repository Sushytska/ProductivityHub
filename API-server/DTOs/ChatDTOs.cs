using System.ComponentModel.DataAnnotations;

namespace ProductivityHub.DTOs
{
    public class ChatDTOs
    {
        public record ChatRequest([Required, MinLength(1), MaxLength(2000)] string Question);

        public record ChatResponse(string Answer, DateTime CreatedDate, IReadOnlyList<ChatSourceResponse> Sources);

        public record ChatSourceResponse(Guid SourceId, string SourceTitle, string SourceType, int ChunkIndex);

        public record ChatStreamMetaEvent(IReadOnlyList<ChatSourceResponse> Sources);

        public record ChatStreamTokenEvent(string Text);

        public record ChatStreamErrorEvent(string Message);
    }
}
