using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductivityHub.Models;
using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class AnthropicChatServiceTests
{
    private static IReadOnlyList<NoteChunk> OneChunk()
    {
        var note = new Note { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Note", Content = "..." };
        return new[] { new NoteChunk { Id = Guid.NewGuid(), NoteId = note.Id, ChunkText = "text", ChunkIndex = 0, Note = note } };
    }

    private static AnthropicChatService CreateSut(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("https://api.anthropic.test")
        };
        return new AnthropicChatService(httpClient, Options.Create(new AnthropicOptions()), NullLogger<AnthropicChatService>.Instance);
    }

    private static HttpResponseMessage SseResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static async Task<List<string>> CollectTokensAsync(IAsyncEnumerable<string> source)
    {
        var tokens = new List<string>();
        await foreach (var token in source)
        {
            tokens.Add(token);
        }

        return tokens;
    }

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

    [Fact]
    public async Task StreamAnswerAsync_MessageStopFrame_YieldsDeltasAndCompletes()
    {
        var body =
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hel\"}}\n\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"lo\"}}\n\n" +
            "event: message_stop\n" +
            "data: {\"type\":\"message_stop\"}\n\n";
        var sut = CreateSut(_ => SseResponse(body));

        var tokens = await CollectTokensAsync(sut.StreamAnswerAsync("Q?", OneChunk()));

        Assert.Equal(new[] { "Hel", "lo" }, tokens);
    }

    [Fact]
    public async Task StreamAnswerAsync_StreamEndsWithoutMessageStop_ThrowsAfterYieldingPartialTokens()
    {
        // Simulates a dropped connection: the HTTP body ends cleanly but Anthropic never
        // sent its terminal frame, so the partial answer must not be treated as complete.
        var body =
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hel\"}}\n\n";
        var sut = CreateSut(_ => SseResponse(body));

        var tokens = new List<string>();
        var ex = await Assert.ThrowsAsync<ChatGenerationException>(async () =>
        {
            await foreach (var token in sut.StreamAnswerAsync("Q?", OneChunk()))
            {
                tokens.Add(token);
            }
        });

        Assert.Equal(new[] { "Hel" }, tokens);
        Assert.Contains("before completion", ex.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_ErrorFrame_ThrowsWithAnthropicMessage()
    {
        var body = "event: error\ndata: {\"type\":\"error\",\"error\":{\"message\":\"boom\"}}\n\n";
        var sut = CreateSut(_ => SseResponse(body));

        var ex = await Assert.ThrowsAsync<ChatGenerationException>(
            async () => await CollectTokensAsync(sut.StreamAnswerAsync("Q?", OneChunk())));

        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_NonSuccessStatusCode_ThrowsBeforeYieldingTokens()
    {
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("server error")
        });

        await Assert.ThrowsAsync<ChatGenerationException>(
            async () => await CollectTokensAsync(sut.StreamAnswerAsync("Q?", OneChunk())));
    }

    [Fact]
    public async Task StreamAnswerAsync_NoChunks_YieldsCannedResponseWithoutTouchingNetwork()
    {
        var called = false;
        var sut = CreateSut(_ => { called = true; return SseResponse(""); });

        var tokens = await CollectTokensAsync(sut.StreamAnswerAsync("Q?", Array.Empty<NoteChunk>()));

        Assert.Equal(new[] { AnthropicChatService.NoContextAnswer }, tokens);
        Assert.False(called);
    }
}
