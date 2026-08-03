using static ProductivityHub.DTOs.ChatDTOs;

namespace ProductivityHub.Services
{
    public abstract record ChatStreamEvent
    {
        public sealed record Meta(IReadOnlyList<ChatSourceResponse> Sources) : ChatStreamEvent;

        public sealed record Token(string Text) : ChatStreamEvent;

        public sealed record Done : ChatStreamEvent;

        public sealed record Error(string Message) : ChatStreamEvent;
    }
}
