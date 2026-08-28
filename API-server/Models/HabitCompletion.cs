namespace ProductivityHub.Models
{
    public class HabitCompletion
    {
        public Guid Id { get; set; }

        public Guid HabitId { get; set; }

        public DateOnly Date { get; set; }

        public Habit Habit { get; set; } = null!;
    }
}
