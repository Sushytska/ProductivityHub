using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ProductivityHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskEmbeddingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "Tasks",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingAttempts",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingError",
                table: "Tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingStatus",
                table: "Tasks",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Embedding",
                table: "Tasks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteChunks_Embedding",
                table: "NoteChunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_Embedding",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_NoteChunks_Embedding",
                table: "NoteChunks");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EmbeddingAttempts",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EmbeddingError",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EmbeddingStatus",
                table: "Tasks");
        }
    }
}
