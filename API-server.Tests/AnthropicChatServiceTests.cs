using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductivityHub.Models;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class AnthropicChatServiceTests
{
    [Fact]
    public void BuildUserContent_IncludesQuestionAndAllChunkText()
    {
        var noteA = new Note { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Note A", Content = "..." };
        var noteB = new Note { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Note B", Content = "..." };
        var chunks = new List<NoteChunk>
        {
            new() { Id = Guid.NewGuid(), NoteId = noteA.Id, ChunkText = "First chunk text", ChunkIndex = 0, Note = noteA },
            new() { Id = Guid.NewGuid(), NoteId = noteB.Id, ChunkText = "Second chunk text", ChunkIndex = 0, Note = noteB },
        };

        var result = AnthropicChatService.BuildUserContent("What is X?", chunks);

        Assert.Contains("Note A", result);
        Assert.Contains("First chunk text", result);
        Assert.Contains("Note B", result);
        Assert.Contains("Second chunk text", result);
        Assert.Contains("What is X?", result);
    }

    [Fact]
    public async Task GetAnswerAsync_NoChunks_ReturnsCannedResponseWithoutTouchingNetwork()
    {
        // HttpClient is never invoked — GetAnswerAsync short-circuits before any HTTP call
        // when there are no context chunks, so this proves no network access is required.
        using var httpClient = new HttpClient();
        var options = Options.Create(new AnthropicOptions());
        var sut = new AnthropicChatService(httpClient, options, NullLogger<AnthropicChatService>.Instance);

        var result = await sut.GetAnswerAsync("Anything", Array.Empty<NoteChunk>());

        Assert.Equal(AnthropicChatService.NoContextAnswer, result);
    }
}
