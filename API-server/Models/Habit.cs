namespace ProductivityHub.Models
{
    public class Habit
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public List<HabitCompletion> Completions { get; set; } = new List<HabitCompletion>();
    }
}
