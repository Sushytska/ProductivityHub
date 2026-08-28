using System.ComponentModel.DataAnnotations;

namespace ProductivityHub.DTOs
{
    public class HabitDTOs
    {
        public record CreateHabitRequest([MaxLength(200)] string Name, [MaxLength(2000)] string? Description);

        public record UpdateHabitRequest([MaxLength(200)] string Name, [MaxLength(2000)] string? Description);

        public record ToggleCompletionRequest(DateOnly Date, DateOnly? AsOfDate = null);

        public record HabitResponse(
            Guid Id,
            string Name,
            string? Description,
            DateTime CreatedDate,
            IReadOnlyList<DateOnly> CompletedDates,
            int CurrentStreak,
            int LongestStreak);
    }
}
