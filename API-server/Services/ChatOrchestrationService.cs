using System.Runtime.CompilerServices;
using System.Text;
using ProductivityHub.Database;
using ProductivityHub.Models;
using static ProductivityHub.DTOs.ChatDTOs;

namespace ProductivityHub.Services
{
    public class ChatOrchestrationService
    {
        private const int DefaultTopK = 5;

        private readonly AppDbContext _db;
        private readonly IRagService _ragService;
        private readonly IChatService _chatService;
        private readonly ILogger<ChatOrchestrationService> _logger;

        public ChatOrchestrationService(AppDbContext db, IRagService ragService, IChatService chatService, ILogger<ChatOrchestrationService> logger)
        {
            _db = db;
            _ragService = ragService;
            _chatService = chatService;
            _logger = logger;
        }

        public async Task<ChatResponse> AskAsync(Guid userId, ChatRequest request, CancellationToken cancellationToken)
        {
            var chunks = await _ragService.GetRelevantChunksAsync(userId, request.Question, DefaultTopK, cancellationToken);
            var answer = await _chatService.GetAnswerAsync(request.Question, chunks, cancellationToken);

            var now = DateTime.UtcNow;
            _db.ChatMessages.AddRange(
                new ChatMessage { Id = Guid.NewGuid(), UserId = userId, Role = ChatRoles.User, Message = request.Question, CreatedDate = now },
                new ChatMessage { Id = Guid.NewGuid(), UserId = userId, Role = ChatRoles.Assistant, Message = answer, CreatedDate = now });

            // Deliberately NOT wrapped/swallowed (unlike TryEnqueueEmbedding in NoteService):
            // a failed save here means the user's chat history silently diverges from what
            // they were shown, which is worse than a failed request they can retry.
            await _db.SaveChangesAsync(cancellationToken);

            return new ChatResponse(
                answer,
                now,
                chunks.Select(c => new ChatSourceResponse(c.NoteId, c.Note.Title, c.ChunkIndex)).ToList());
        }

        public async IAsyncEnumerable<ChatStreamEvent> AskStreamingAsync(
            Guid userId, ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var chunks = await _ragService.GetRelevantChunksAsync(userId, request.Question, DefaultTopK, cancellationToken);
            var sources = chunks.Select(c => new ChatSourceResponse(c.NoteId, c.Note.Title, c.ChunkIndex)).ToList();
            yield return new ChatStreamEvent.Meta(sources);

            var answerBuilder = new StringBuilder();
            await foreach (var delta in _chatService.StreamAnswerAsync(request.Question, chunks, cancellationToken))
            {
                answerBuilder.Append(delta);
                yield return new ChatStreamEvent.Token(delta);
            }

            var answer = answerBuilder.ToString();
            var now = DateTime.UtcNow;
            Exception? persistError = null;
            try
            {
                _db.ChatMessages.AddRange(
                    new ChatMessage { Id = Guid.NewGuid(), UserId = userId, Role = ChatRoles.User, Message = request.Question, CreatedDate = now },
                    new ChatMessage { Id = Guid.NewGuid(), UserId = userId, Role = ChatRoles.Assistant, Message = answer, CreatedDate = now });
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                persistError = ex;
            }

            if (persistError is not null)
            {
                _logger.LogError(persistError, "Failed to persist chat messages after streaming to user {UserId}.", userId);
                yield return new ChatStreamEvent.Error("The answer was streamed but could not be saved.");
                yield break;
            }

            yield return new ChatStreamEvent.Done();
        }
    }
}
