using ProductivityHub.Services;

namespace ProductivityHub.Tests;

public class SseFrameParserTests
{
    private static async IAsyncEnumerable<string> ToAsyncLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }

        await Task.CompletedTask;
    }

    private static async Task<List<string>> CollectAsync(IAsyncEnumerable<string> source)
    {
        var results = new List<string>();
        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }

    [Fact]
    public async Task ExtractDataPayloads_SingleLineFramesSeparatedByBlankLine_ReturnsEachPayload()
    {
        var lines = new[]
        {
            "event: content_block_delta",
            "data: {\"type\":\"content_block_delta\"}",
            "",
            "event: message_stop",
            "data: {\"type\":\"message_stop\"}",
            ""
        };

        var payloads = await CollectAsync(SseFrameParser.ExtractDataPayloads(ToAsyncLines(lines)));

        Assert.Equal(
            new[] { "{\"type\":\"content_block_delta\"}", "{\"type\":\"message_stop\"}" },
            payloads);
    }

    [Fact]
    public async Task ExtractDataPayloads_MultiLineDataContinuation_JoinsWithNewline()
    {
        var lines = new[]
        {
            "data: line one",
            "data: line two",
            ""
        };

        var payloads = await CollectAsync(SseFrameParser.ExtractDataPayloads(ToAsyncLines(lines)));

        Assert.Equal(new[] { "line one\nline two" }, payloads);
    }

    [Fact]
    public async Task ExtractDataPayloads_TrailingFrameWithNoClosingBlankLine_IsStillReturned()
    {
        var lines = new[]
        {
            "data: {\"type\":\"message_stop\"}"
        };

        var payloads = await CollectAsync(SseFrameParser.ExtractDataPayloads(ToAsyncLines(lines)));

        Assert.Equal(new[] { "{\"type\":\"message_stop\"}" }, payloads);
    }

    [Fact]
    public async Task ExtractDataPayloads_ErrorFrame_PassesThroughUnmodified()
    {
        var lines = new[]
        {
            "event: error",
            "data: {\"type\":\"error\",\"error\":{\"message\":\"boom\"}}",
            ""
        };

        var payloads = await CollectAsync(SseFrameParser.ExtractDataPayloads(ToAsyncLines(lines)));

        Assert.Equal(new[] { "{\"type\":\"error\",\"error\":{\"message\":\"boom\"}}" }, payloads);
    }

    [Fact]
    public async Task ExtractDataPayloads_BlankLinesWithNoDataBetween_AreIgnored()
    {
        var lines = new[]
        {
            "",
            "",
            "data: only",
            ""
        };

        var payloads = await CollectAsync(SseFrameParser.ExtractDataPayloads(ToAsyncLines(lines)));

        Assert.Equal(new[] { "only" }, payloads);
    }
}
