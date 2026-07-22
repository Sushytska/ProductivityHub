using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class NoteChunkerTests
{
    private readonly NoteChunker _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void Chunk_EmptyOrWhitespaceContent_ReturnsNoChunks(string content)
    {
        Assert.Empty(_sut.Chunk(content));
    }

    [Fact]
    public void Chunk_ContentShorterThanChunkSize_ReturnsSingleChunkWithAllWords()
    {
        var words = Enumerable.Range(0, 10).Select(i => $"word{i}");
        var content = string.Join(' ', words);

        var chunks = _sut.Chunk(content);

        Assert.Single(chunks);
        Assert.Equal(content, chunks[0]);
    }

    [Fact]
    public void Chunk_ContentExactlyChunkSize_ReturnsSingleChunk()
    {
        var words = Enumerable.Range(0, NoteChunker.ChunkSizeWords).Select(i => $"w{i}");
        var content = string.Join(' ', words);

        var chunks = _sut.Chunk(content);

        Assert.Single(chunks);
        Assert.Equal(NoteChunker.ChunkSizeWords, chunks[0].Split(' ').Length);
    }

    [Fact]
    public void Chunk_ContentLongerThanChunkSize_ProducesMultipleChunksWithExpectedOverlap()
    {
        // 1200 words: with a 500-word window / 400-word step this should produce
        // chunks starting at 0, 400, 800 -> 3 chunks (last one 400 words: [800,1200)).
        var totalWords = 1200;
        var words = Enumerable.Range(0, totalWords).Select(i => $"w{i}").ToArray();
        var content = string.Join(' ', words);

        var chunks = _sut.Chunk(content);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(NoteChunker.ChunkSizeWords, chunks[0].Split(' ').Length);
        Assert.Equal(NoteChunker.ChunkSizeWords, chunks[1].Split(' ').Length);
        Assert.Equal(400, chunks[2].Split(' ').Length);

        // Overlap correctness: the last 100 words of chunk[i] equal the first 100 words of chunk[i+1].
        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var tailOfCurrent = chunks[i].Split(' ').TakeLast(NoteChunker.OverlapWords);
            var headOfNext = chunks[i + 1].Split(' ').Take(NoteChunker.OverlapWords);
            Assert.Equal(tailOfCurrent, headOfNext);
        }

        // No content lost or reordered: chunk[0] starts at w0, chunk[last] ends at the final word.
        Assert.StartsWith("w0 ", chunks[0]);
        Assert.EndsWith($"w{totalWords - 1}", chunks[^1]);
    }

    [Fact]
    public void Chunk_NormalizesInternalWhitespaceAndNewlines()
    {
        var content = "alpha\nbeta   gamma\t\tdelta";

        var chunks = _sut.Chunk(content);

        Assert.Single(chunks);
        Assert.Equal("alpha beta gamma delta", chunks[0]);
    }
}
