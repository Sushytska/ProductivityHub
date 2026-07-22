using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ProductivityHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingPipelineColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Notes");

            migrationBuilder.AddColumn<int>(
                name: "EmbeddingAttempts",
                table: "Notes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingError",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingStatus",
                table: "Notes",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "NoteChunks",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingAttempts",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "EmbeddingError",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "EmbeddingStatus",
                table: "Notes");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "Notes",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "NoteChunks",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);
        }
    }
}
