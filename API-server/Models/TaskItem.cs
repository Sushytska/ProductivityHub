using Pgvector;

namespace ProductivityHub.Models
{
    public class TaskItem
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsCompleted { get; set; } = false;

        public DateOnly? DueDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public EmbeddingStatus EmbeddingStatus { get; set; } = EmbeddingStatus.Pending;

        public int EmbeddingAttempts { get; set; } = 0;

        public string? EmbeddingError { get; set; }

        public Vector? Embedding { get; set; }

        // Shared by both TaskEmbeddingProcessor (as part of what gets embedded) and RagService
        // (as the RagSourceItem.Text shown to Claude) — deliberately the same text in both
        // places, so retrieval and generation never drift: a question like "when is my
        // electricity bill due" only works if the due date is present in what gets searched
        // AND in what the model actually reads.
        public string BuildContextText()
        {
            var status = IsCompleted ? "Completed" : "Not completed";
            var due = DueDate.HasValue ? DueDate.Value.ToString("yyyy-MM-dd") : "No due date";
            var description = string.IsNullOrWhiteSpace(Description) ? "(no description)" : Description;

            return $"Status: {status}\nDue: {due}\n{description}";
        }
    }
}
