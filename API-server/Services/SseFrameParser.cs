namespace ProductivityHub.Services
{
    /// <summary>
    /// Extracts the "data:" payload of each blank-line-delimited SSE frame from a
    /// sequence of raw lines. Ignores "event:"/"id:"/comment lines deliberately —
    /// for the Anthropic wire format, the JSON payload's own "type" field mirrors
    /// the event name, so the "event:" line carries no information the payload doesn't.
    /// Operates on IAsyncEnumerable so the same logic drives both the real streamed
    /// read (line-by-line off the network) and unit tests (a canned in-memory sequence).
    /// </summary>
    internal static class SseFrameParser
    {
        public static async IAsyncEnumerable<string> ExtractDataPayloads(IAsyncEnumerable<string> lines)
        {
            var dataLines = new List<string>();

            await foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    if (dataLines.Count == 0)
                    {
                        continue;
                    }

                    yield return string.Join("\n", dataLines);
                    dataLines.Clear();
                    continue;
                }

                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    dataLines.Add(line.Length > 5 && line[5] == ' ' ? line[6..] : line[5..]);
                }
            }

            if (dataLines.Count > 0)
            {
                yield return string.Join("\n", dataLines);
            }
        }
    }
}
