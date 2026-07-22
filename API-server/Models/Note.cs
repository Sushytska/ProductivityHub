namespace ProductivityHub.Models
{
    public class Note
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Title { get; set; } = string.Empty;

        public required string Content { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public EmbeddingStatus EmbeddingStatus { get; set; } = EmbeddingStatus.Pending;

        public int EmbeddingAttempts { get; set; } = 0;

        public string? EmbeddingError { get; set; }

        public List<NoteChunk> Chunks { get; set; } = new List<NoteChunk>();
    }
}
