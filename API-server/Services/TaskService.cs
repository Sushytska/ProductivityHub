using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;
using static ProductivityHub.DTOs.TaskDTOs;

namespace ProductivityHub.Services
{
    public class TaskService
    {
        private readonly AppDbContext _db;
        private readonly ITaskEmbeddingQueue _embeddingQueue;
        private readonly ILogger<TaskService> _logger;

        public TaskService(AppDbContext db, ITaskEmbeddingQueue embeddingQueue, ILogger<TaskService> logger)
        {
            _db = db;
            _embeddingQueue = embeddingQueue;
            _logger = logger;
        }

        public async Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request)
        {
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = request.Title,
                Description = request.Description,
                IsCompleted = request.IsCompleted,
                DueDate = request.DueDate,
                CreatedDate = DateTime.UtcNow
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            TryEnqueueEmbedding(task.Id);

            return ToResponse(task);
        }

        public async Task<List<TaskResponse>> GetAllAsync(Guid userId)
        {
            var tasks = await _db.Tasks
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.IsCompleted)
                .ThenByDescending(t => t.CreatedDate)
                .ToListAsync();

            return tasks.Select(ToResponse).ToList();
        }

        public async Task<TaskResponse?> GetByIdAsync(Guid userId, Guid taskId)
        {
            var task = await _db.Tasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            return task == null ? null : ToResponse(task);
        }

        public async Task<TaskResponse?> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request)
        {
            var task = await _db.Tasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if (task == null)
            {
                return null;
            }

            task.Title = request.Title;
            task.Description = request.Description;
            task.IsCompleted = request.IsCompleted;
            task.DueDate = request.DueDate;
            task.EmbeddingStatus = EmbeddingStatus.Pending;
            task.EmbeddingAttempts = 0;
            task.EmbeddingError = null;

            await _db.SaveChangesAsync();

            TryEnqueueEmbedding(task.Id);

            return ToResponse(task);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid taskId)
        {
            var task = await _db.Tasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if (task == null)
            {
                return false;
            }

            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync();

            return true;
        }

        private void TryEnqueueEmbedding(Guid taskId)
        {
            // The task is already committed at this point (EmbeddingStatus=Pending), so a queue
            // failure here must not fail the request. StrandedTaskReconciler picks up any task
            // left in Pending with no queue entry the next time the app starts.
            try
            {
                _embeddingQueue.Enqueue(taskId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue task {TaskId} for embedding; it will be picked up on the next reconciliation pass.", taskId);
            }
        }

        private static TaskResponse ToResponse(TaskItem task) =>
            new(task.Id, task.Title, task.Description, task.IsCompleted, task.DueDate, task.CreatedDate);
    }
}
