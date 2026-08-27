namespace ProductivityHub.Services
{
    /// <summary>
    /// A single retrieved-and-ranked piece of context for chat, regardless of whether it came
    /// from a Note chunk or a Task. SourceType is "Note" or "Task". ChunkIndex is always 0 for
    /// tasks (they aren't chunked). Distance is the raw cosine distance used to merge/rank
    /// candidates from both sources together before capping to the final topK.
    /// </summary>
    public record RagSourceItem(string SourceType, Guid SourceId, string Title, string Text, int ChunkIndex, double Distance);
}
