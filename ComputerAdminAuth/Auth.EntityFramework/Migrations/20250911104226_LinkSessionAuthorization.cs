using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class LinkSessionAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorizationId",
                table: "user_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_AuthorizationId",
                table: "user_sessions",
                column: "AuthorizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_sessions_AuthorizationId",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "AuthorizationId",
                table: "user_sessions");
        }
    }
}
