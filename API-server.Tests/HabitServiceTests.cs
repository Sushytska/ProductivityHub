using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;
using ProductivityHub.Services;
using static ProductivityHub.DTOs.HabitDTOs;

namespace ProductivityHub.Tests;

public class HabitServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static HabitService CreateSut(AppDbContext db) => new(db);

    private static CreateHabitRequest Request(string name = "Name", string? description = "Description") =>
        new(name, description);

    [Fact]
    public async Task CreateAsync_SetsOwnerToCallingUser()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();

        var response = await sut.CreateAsync(userId, Request("Name", "Description"));

        var stored = await db.Habits.SingleAsync(h => h.Id == response.Id);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("Name", stored.Name);
        Assert.Equal("Description", stored.Description);
        Assert.Empty(response.CompletedDates);
        Assert.Equal(0, response.CurrentStreak);
        Assert.Equal(0, response.LongestStreak);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyCallingUsersHabits()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await sut.CreateAsync(userA, Request("A1"));
        await sut.CreateAsync(userA, Request("A2"));
        await sut.CreateAsync(userB, Request("B1"));

        var result = await sut.GetAllAsync(userA);

        Assert.Equal(2, result.Count);
        Assert.All(result, h => Assert.Contains(h.Name, new[] { "A1", "A2" }));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOwnHabit()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request("Mine"));

        var result = await sut.GetByIdAsync(userId, created.Id);

        Assert.NotNull(result);
        Assert.Equal("Mine", result!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissingHabit()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForAnotherUsersHabit()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, Request("Private"));

        var result = await sut.GetByIdAsync(intruder, created.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesOwnHabit()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request("Old", "Old desc"));

        var result = await sut.UpdateAsync(userId, created.Id, new UpdateHabitRequest("New", "New desc"));

        Assert.NotNull(result);
        Assert.Equal("New", result!.Name);
        Assert.Equal("New desc", result.Description);

        var stored = await db.Habits.SingleAsync(h => h.Id == created.Id);
        Assert.Equal("New", stored.Name);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForMissingHabit()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateHabitRequest("X", null));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForAnotherUsersHabitAndDoesNotModify()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, Request("Original"));

        var result = await sut.UpdateAsync(intruder, created.Id, new UpdateHabitRequest("Hacked", null));

        Assert.Null(result);
        var stored = await db.Habits.SingleAsync(h => h.Id == created.Id);
        Assert.Equal("Original", stored.Name);
    }

    [Fact]
    public async Task DeleteAsync_DeletesOwnHabit()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request("ToDelete"));

        var result = await sut.DeleteAsync(userId, created.Id);

        Assert.True(result);
        Assert.False(await db.Habits.AnyAsync(h => h.Id == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForMissingHabit()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);

        var result = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForAnotherUsersHabitAndDoesNotDelete()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, Request("Keep"));

        var result = await sut.DeleteAsync(intruder, created.Id);

        Assert.False(result);
        Assert.True(await db.Habits.AnyAsync(h => h.Id == created.Id));
    }

    [Fact]
    public async Task ToggleCompletionAsync_TogglesOnThenOff()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        var date = new DateOnly(2026, 1, 1);

        var toggledOn = await sut.ToggleCompletionAsync(userId, created.Id, date);
        Assert.NotNull(toggledOn);
        Assert.Contains(date, toggledOn!.CompletedDates);

        var toggledOff = await sut.ToggleCompletionAsync(userId, created.Id, date);
        Assert.NotNull(toggledOff);
        Assert.DoesNotContain(date, toggledOff!.CompletedDates);
    }

    [Fact]
    public async Task ToggleCompletionAsync_AnotherUsersHabit_ReturnsNullAndDoesNotModify()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var created = await sut.CreateAsync(owner, Request());
        var date = new DateOnly(2026, 1, 1);

        var result = await sut.ToggleCompletionAsync(intruder, created.Id, date);

        Assert.Null(result);
        var stored = await db.Habits.Include(h => h.Completions).SingleAsync(h => h.Id == created.Id);
        Assert.Empty(stored.Completions);
    }

    [Fact]
    public async Task ToggleCompletionAsync_RejectsFutureDate_LeavesStateUnchanged()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        var today = new DateOnly(2026, 1, 1);
        var future = today.AddDays(1);

        var result = await sut.ToggleCompletionAsync(userId, created.Id, future, today);

        Assert.NotNull(result);
        Assert.DoesNotContain(future, result!.CompletedDates);
        Assert.False(await db.HabitCompletions.AnyAsync(c => c.HabitId == created.Id));
    }

    [Fact]
    public async Task ToggleCompletionAsync_UsesAsOfDateForStreakInsteadOfServerToday()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        // A client whose local calendar day is already ahead of server UTC "today".
        var clientToday = new DateOnly(2026, 1, 2);

        var result = await sut.ToggleCompletionAsync(userId, created.Id, clientToday, clientToday);

        Assert.NotNull(result);
        Assert.Equal(1, result!.CurrentStreak);
    }

    // ToggleCompletionAsync's DbUpdateException handling (the fix for a race between two
    // concurrent toggle requests hitting the unique (HabitId, Date) index) can't be
    // exercised here: the EF Core InMemory provider does not enforce unique indexes at all
    // (confirmed experimentally — inserting two rows with the same (HabitId, Date), even
    // via two separate DbContexts and separate SaveChangesAsync calls, raises no exception).
    // Verified manually instead, against real Postgres: fired 10 truly concurrent
    // `POST /api/Habits/{id}/toggle` requests for the same day (once against a
    // not-yet-completed day, once against an already-completed one). Every request
    // returned 200 with a consistent CompletedDates list; the API logs show the expected
    // Npgsql 23505 unique-violation on the losing request(s) of each race, caught by the
    // DbUpdateException handler — none surfaced as an unhandled exception/500, and exactly
    // one row (or zero, for the remove race) existed in HabitCompletions afterward.

    // DeleteAsync (above) does not Include(h => h.Completions) before removing a habit —
    // cascade deletion of its HabitCompletion rows relies entirely on the FK's
    // ON DELETE CASCADE at the database level (AppDbContext's HasMany/WithOne plus the
    // AddHabitsTables migration, confirmed via `confdeltype = 'c'` on
    // FK_HabitCompletions_Habits_HabitId). The EF Core InMemory provider has no real FK
    // enforcement, so that specific path can't be meaningfully unit-tested here — same
    // caveat as RagService's pgvector CosineDistance query elsewhere in this suite.
    // Verified manually instead: created a habit, toggled a completion, deleted the habit,
    // and confirmed via `docker exec ... psql` that the HabitCompletions row was gone.

    [Fact]
    public async Task Streak_NConsecutiveDaysEndingToday_ReportsCurrentAndLongest()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        HabitResponse? response = null;
        for (var i = 0; i < 5; i++)
        {
            response = await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-i));
        }

        Assert.Equal(5, response!.CurrentStreak);
        Assert.Equal(5, response.LongestStreak);
    }

    [Fact]
    public async Task Streak_PreservedWhenTodayNotYetDoneButYesterdayWas()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-1));
        var response = await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-2));

        Assert.Equal(2, response!.CurrentStreak);
    }

    [Fact]
    public async Task Streak_ZeroWhenTodayAndYesterdayMissing()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-2));

        Assert.Equal(0, response!.CurrentStreak);
    }

    [Fact]
    public async Task Streak_GapBreaksCurrentStreak()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await sut.ToggleCompletionAsync(userId, created.Id, today);
        await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-1));
        // Gap at today-2 breaks the run before this older, isolated day.
        var response = await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-3));

        Assert.Equal(2, response!.CurrentStreak);
    }

    [Fact]
    public async Task Streak_LongestStreakCorrectEvenWhenCurrentStreakIsShorter()
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var userId = Guid.NewGuid();
        var created = await sut.CreateAsync(userId, Request());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // A historical 10-day run, two weeks ago.
        for (var i = 20; i >= 11; i--)
        {
            await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-i));
        }

        // A shorter, current 2-day run.
        await sut.ToggleCompletionAsync(userId, created.Id, today);
        var response = await sut.ToggleCompletionAsync(userId, created.Id, today.AddDays(-1));

        Assert.Equal(2, response!.CurrentStreak);
        Assert.Equal(10, response.LongestStreak);
    }
}
