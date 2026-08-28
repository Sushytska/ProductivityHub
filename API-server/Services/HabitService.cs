using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;
using static ProductivityHub.DTOs.HabitDTOs;

namespace ProductivityHub.Services
{
    public class HabitService
    {
        private readonly AppDbContext _db;

        public HabitService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<HabitResponse> CreateAsync(Guid userId, CreateHabitRequest request)
        {
            var habit = new Habit
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = request.Name,
                Description = request.Description,
                CreatedDate = DateTime.UtcNow
            };

            _db.Habits.Add(habit);
            await _db.SaveChangesAsync();

            return BuildResponse(habit);
        }

        public async Task<List<HabitResponse>> GetAllAsync(Guid userId, DateOnly? asOfDate = null)
        {
            var habits = await _db.Habits
                .Include(h => h.Completions)
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedDate)
                .ToListAsync();

            return habits.Select(h => BuildResponse(h, asOfDate)).ToList();
        }

        public async Task<HabitResponse?> GetByIdAsync(Guid userId, Guid habitId, DateOnly? asOfDate = null)
        {
            var habit = await _db.Habits
                .Include(h => h.Completions)
                .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

            return habit == null ? null : BuildResponse(habit, asOfDate);
        }

        public async Task<HabitResponse?> UpdateAsync(Guid userId, Guid habitId, UpdateHabitRequest request)
        {
            var habit = await _db.Habits
                .Include(h => h.Completions)
                .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

            if (habit == null)
            {
                return null;
            }

            habit.Name = request.Name;
            habit.Description = request.Description;

            await _db.SaveChangesAsync();

            return BuildResponse(habit);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid habitId)
        {
            var habit = await _db.Habits
                .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

            if (habit == null)
            {
                return false;
            }

            _db.Habits.Remove(habit);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<HabitResponse?> ToggleCompletionAsync(Guid userId, Guid habitId, DateOnly date, DateOnly? asOfDate = null)
        {
            var habit = await _db.Habits
                .FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);

            if (habit == null)
            {
                return null;
            }

            var today = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // Reject toggles for dates beyond the caller's "today" — otherwise a direct API
            // call could pre-mark future days and inflate LongestStreak, since that scan has
            // no other way to distinguish a real run from one that hasn't happened yet.
            if (date > today)
            {
                var unchangedDates = await _db.HabitCompletions
                    .Where(c => c.HabitId == habitId)
                    .Select(c => c.Date)
                    .ToListAsync();
                return BuildResponse(habit, unchangedDates, today);
            }

            // Operate on HabitCompletions directly rather than mutating habit.Completions
            // in place — adding/removing through an already-tracked nav collection after a
            // separate Include() confuses the EF Core InMemory provider's change tracker.
            var existing = await _db.HabitCompletions
                .FirstOrDefaultAsync(c => c.HabitId == habitId && c.Date == date);

            if (existing != null)
            {
                _db.HabitCompletions.Remove(existing);
            }
            else
            {
                _db.HabitCompletions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habitId, Date = date });
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request for the same (HabitId, Date) already committed first —
                // the unique index rejected this one, or this one tried to remove a row the
                // other request already removed. Either way, discard this call's pending
                // change and fall through to read back whatever state actually won the race.
                _db.ChangeTracker.Clear();
            }

            var completedDates = await _db.HabitCompletions
                .Where(c => c.HabitId == habitId)
                .Select(c => c.Date)
                .ToListAsync();

            return BuildResponse(habit, completedDates, today);
        }

        private static HabitResponse BuildResponse(Habit habit, DateOnly? asOfDate = null) =>
            BuildResponse(habit, habit.Completions.Select(c => c.Date), asOfDate);

        private static HabitResponse BuildResponse(Habit habit, IEnumerable<DateOnly> dates, DateOnly? asOfDate = null)
        {
            var sortedDates = dates.OrderBy(d => d).ToList();
            var today = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var (currentStreak, longestStreak) = ComputeStreaks(sortedDates, today);

            return new HabitResponse(
                habit.Id,
                habit.Name,
                habit.Description,
                habit.CreatedDate,
                sortedDates,
                currentStreak,
                longestStreak);
        }

        private static (int CurrentStreak, int LongestStreak) ComputeStreaks(IReadOnlyList<DateOnly> sortedDates, DateOnly today)
        {
            if (sortedDates.Count == 0)
            {
                return (0, 0);
            }

            var dateSet = new HashSet<DateOnly>(sortedDates);

            // Missing *today* doesn't zero the streak until the day is actually over —
            // walk back from today if it's done, otherwise from yesterday (so the streak
            // doesn't flash to 0 every morning before the user has checked in yet). "today"
            // is caller-supplied (the client's local calendar day) rather than always
            // recomputed from server UTC, so this agrees with the week-strip the client shows.
            var currentStreak = 0;
            var cursor = dateSet.Contains(today) ? today : today.AddDays(-1);
            while (dateSet.Contains(cursor))
            {
                currentStreak++;
                cursor = cursor.AddDays(-1);
            }

            // Longest streak scans the full history for the longest run of consecutive
            // dates, independent of whether that run includes today.
            var longestStreak = 1;
            var runLength = 1;
            for (var i = 1; i < sortedDates.Count; i++)
            {
                runLength = sortedDates[i] == sortedDates[i - 1].AddDays(1) ? runLength + 1 : 1;
                longestStreak = Math.Max(longestStreak, runLength);
            }

            return (currentStreak, longestStreak);
        }
    }
}
