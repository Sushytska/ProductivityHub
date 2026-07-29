using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductivityHub.Migrations
{
    /// <inheritdoc />
    public partial class RenameNoteCunksToNoteChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NoteCunks_Notes_NoteId",
                table: "NoteCunks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NoteCunks",
                table: "NoteCunks");

            migrationBuilder.RenameTable(
                name: "NoteCunks",
                newName: "NoteChunks");

            migrationBuilder.RenameIndex(
                name: "IX_NoteCunks_NoteId",
                table: "NoteChunks",
                newName: "IX_NoteChunks_NoteId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NoteChunks",
                table: "NoteChunks",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NoteChunks_Notes_NoteId",
                table: "NoteChunks",
                column: "NoteId",
                principalTable: "Notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NoteChunks_Notes_NoteId",
                table: "NoteChunks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NoteChunks",
                table: "NoteChunks");

            migrationBuilder.RenameTable(
                name: "NoteChunks",
                newName: "NoteCunks");

            migrationBuilder.RenameIndex(
                name: "IX_NoteChunks_NoteId",
                table: "NoteCunks",
                newName: "IX_NoteCunks_NoteId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NoteCunks",
                table: "NoteCunks",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NoteCunks_Notes_NoteId",
                table: "NoteCunks",
                column: "NoteId",
                principalTable: "Notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
