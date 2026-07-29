using Microsoft.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Models;

namespace ProductivityHub.Tests;

/// <summary>
/// AppDbContext's NoteChunk.Embedding vector column relies on the Npgsql pgvector plugin,
/// which the EF Core InMemory provider used in these tests doesn't support. Ignoring
/// it here keeps unit tests decoupled from a real Postgres instance without touching
/// the production model.
/// </summary>
internal class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NoteChunk>().Ignore(nc => nc.Embedding);
    }
}
