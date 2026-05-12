using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aeges.Storage.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    chat_id = table.Column<long>(type: "INTEGER", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "telegram_project_access",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_project_access", x => new { x.user_id, x.project_id });
                    table.ForeignKey(
                        name: "FK_telegram_project_access_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_telegram_project_access_telegram_users_user_id",
                        column: x => x.user_id,
                        principalTable: "telegram_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telegram_project_group_access",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    project_group_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_project_group_access", x => new { x.user_id, x.project_group_id });
                    table.ForeignKey(
                        name: "FK_telegram_project_group_access_project_groups_project_group_id",
                        column: x => x.project_group_id,
                        principalTable: "project_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_telegram_project_group_access_telegram_users_user_id",
                        column: x => x.user_id,
                        principalTable: "telegram_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_project_access_project_id",
                table: "telegram_project_access",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_project_group_access_project_group_id",
                table: "telegram_project_group_access",
                column: "project_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_users_chat_id",
                table: "telegram_users",
                column: "chat_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_users_role",
                table: "telegram_users",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_users_status",
                table: "telegram_users",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_project_access");

            migrationBuilder.DropTable(
                name: "telegram_project_group_access");

            migrationBuilder.DropTable(
                name: "telegram_users");
        }
    }
}
