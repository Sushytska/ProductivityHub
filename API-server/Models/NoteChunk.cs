using Pgvector;

namespace ProductivityHub.Models
{
    public class NoteChunk
    {
        public Guid Id { get; set; }

        public Guid NoteId { get; set; }

        public required string ChunkText { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        public Vector? Embedding { get; set; }

        public Note Note { get; set; } = null!;
    }
}
