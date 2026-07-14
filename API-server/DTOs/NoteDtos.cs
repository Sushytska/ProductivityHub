namespace ProductivityHub.DTOs
{
    public class NoteDTOs
    {
        public record CreateNoteRequest(string Title, string Content);

        public record UpdateNoteRequest(string Title, string Content);

        public record NoteResponse(Guid Id, string Title, string Content, DateTime CreatedDate);
    }
}
