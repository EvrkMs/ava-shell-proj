using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComputerAdminAuth.Migrations
{
    /// <inheritdoc />
    public partial class FixUniqTelegramId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TelegramEntities_TelegramId",
                table: "TelegramEntities",
                column: "TelegramId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TelegramEntities_TelegramId",
                table: "TelegramEntities");
        }
    }
}
