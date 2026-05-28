using Pgvector;

namespace ProductivityHub.Models
{
    public class Note
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Title { get; set; } = string.Empty;

        public required string Content { get; set; } = string.Empty;

        public Vector? Embedding { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public List<NoteChunk> Chunks { get; set; } = new List<NoteChunk>();


    }
}
