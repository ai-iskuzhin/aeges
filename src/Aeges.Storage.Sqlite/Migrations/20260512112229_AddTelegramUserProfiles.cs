using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aeges.Storage.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramUserProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "first_name",
                table: "telegram_users",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                table: "telegram_users",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "telegram_users",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_users_username",
                table: "telegram_users",
                column: "username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_telegram_users_username",
                table: "telegram_users");

            migrationBuilder.DropColumn(
                name: "first_name",
                table: "telegram_users");

            migrationBuilder.DropColumn(
                name: "last_name",
                table: "telegram_users");

            migrationBuilder.DropColumn(
                name: "username",
                table: "telegram_users");
        }
    }
}
