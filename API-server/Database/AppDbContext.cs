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

        public DbSet<NoteChunk> NoteCunks { get; set; }

        public DbSet<ChatMessage> ChatMessages { get; set; }

        public DbSet<User> Users { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<Note>(entity =>
            {
                entity.Property(n => n.Embedding)
                    .HasColumnType("vector(1536)");

                entity.HasMany(n => n.Chunks)
                    .WithOne(n => n.Note)
                    .OnDelete(DeleteBehavior.Cascade);
            });
                
            modelBuilder.Entity<NoteChunk>()
                .Property(nc => nc.Embedding)
                .HasColumnType("vector(1536)");
        }
    }
}
