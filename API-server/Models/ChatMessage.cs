namespace ProductivityHub.Models
{
    public class ChatMessage
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Role { get; set; } = string.Empty;

        public required string Message { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
