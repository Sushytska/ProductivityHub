namespace ProductivityHub.Services
{
    public class NoteChunker : INoteChunker
    {
        public const int ChunkSizeWords = 500;
        public const int OverlapWords = 100; // 20% of ChunkSizeWords

        public IReadOnlyList<string> Chunk(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Array.Empty<string>();
            }

            var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var totalWords = words.Length;

            if (totalWords <= ChunkSizeWords)
            {
                return new[] { string.Join(' ', words) };
            }

            var chunks = new List<string>();
            var step = ChunkSizeWords - OverlapWords;

            for (var start = 0; start < totalWords; start += step)
            {
                var end = Math.Min(start + ChunkSizeWords, totalWords);
                chunks.Add(string.Join(' ', words[start..end]));

                if (end == totalWords)
                {
                    break;
                }
            }

            return chunks;
        }
    }
}
