using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComputerAdminAuth.Migrations
{
    /// <inheritdoc />
    public partial class FixApply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TelegramEntity_AspNetUsers_UserId",
                table: "TelegramEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TelegramEntity",
                table: "TelegramEntity");

            migrationBuilder.RenameTable(
                name: "TelegramEntity",
                newName: "TelegramEntities");

            migrationBuilder.RenameIndex(
                name: "IX_TelegramEntity_UserId",
                table: "TelegramEntities",
                newName: "IX_TelegramEntities_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TelegramEntities",
                table: "TelegramEntities",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TelegramEntities_AspNetUsers_UserId",
                table: "TelegramEntities",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TelegramEntities_AspNetUsers_UserId",
                table: "TelegramEntities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TelegramEntities",
                table: "TelegramEntities");

            migrationBuilder.RenameTable(
                name: "TelegramEntities",
                newName: "TelegramEntity");

            migrationBuilder.RenameIndex(
                name: "IX_TelegramEntities_UserId",
                table: "TelegramEntity",
                newName: "IX_TelegramEntity_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TelegramEntity",
                table: "TelegramEntity",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TelegramEntity_AspNetUsers_UserId",
                table: "TelegramEntity",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
