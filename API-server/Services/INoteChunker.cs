namespace ProductivityHub.Services
{
    public interface INoteChunker
    {
        IReadOnlyList<string> Chunk(string content);
    }
}
