using ProductivityHub.Models;
using Pgvector.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ProductivityHub.Database
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Note> Notes { get; set; }

        public DbSet<NoteChunk> NoteChunks { get; set; }

        public DbSet<TaskItem> Tasks { get; set; }

        public DbSet<Habit> Habits { get; set; }

        public DbSet<HabitCompletion> HabitCompletions { get; set; }

        public DbSet<ChatMessage> ChatMessages { get; set; }

        public DbSet<User> Users { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<User>(entity =>
            {
                // Register-then-insert has an application-level "email taken" check
                // (AuthService.RegisterAsync), but only a DB-level unique constraint
                // closes the race where two concurrent registrations for the same email
                // both pass that check before either commits.
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<Note>(entity =>
            {
                entity.Property(n => n.EmbeddingStatus)
                    .HasConversion<string>();

                entity.HasMany(n => n.Chunks)
                    .WithOne(n => n.Note)
                    .OnDelete(DeleteBehavior.Cascade);

                // Every ownership-scoped query filters on UserId (there's no FK/nav
                // property to User — see CLAUDE.md) — this is the column each of those
                // queries needs indexed, not Id.
                entity.HasIndex(n => n.UserId);
            });

            modelBuilder.Entity<NoteChunk>(entity =>
            {
                entity.Property(nc => nc.Embedding).HasColumnType("vector(768)");
                entity.HasIndex(nc => nc.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.Property(t => t.EmbeddingStatus).HasConversion<string>();
                entity.Property(t => t.Embedding).HasColumnType("vector(768)");
                entity.HasIndex(t => t.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
                entity.HasIndex(t => t.UserId);
            });

            modelBuilder.Entity<Habit>(entity =>
            {
                entity.HasMany(h => h.Completions)
                    .WithOne(c => c.Habit)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(h => h.UserId);
            });

            modelBuilder.Entity<HabitCompletion>(entity =>
            {
                entity.HasIndex(c => new { c.HabitId, c.Date }).IsUnique();
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasIndex(m => m.UserId);
            });
        }
    }
}
