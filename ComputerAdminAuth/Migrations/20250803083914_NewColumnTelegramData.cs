using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComputerAdminAuth.Migrations
{
    /// <inheritdoc />
    public partial class NewColumnTelegramData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TelegramName",
                table: "TelegramEntities",
                newName: "Username");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "TelegramEntities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginDate",
                table: "TelegramEntities",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "TelegramEntities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "TelegramEntities",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "TelegramEntities");

            migrationBuilder.DropColumn(
                name: "LastLoginDate",
                table: "TelegramEntities");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "TelegramEntities");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "TelegramEntities");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "TelegramEntities",
                newName: "TelegramName");
        }
    }
}
