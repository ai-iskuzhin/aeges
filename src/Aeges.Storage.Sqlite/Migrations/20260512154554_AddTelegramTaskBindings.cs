using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aeges.Storage.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramTaskBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_task_bindings",
                columns: table => new
                {
                    chat_id = table.Column<long>(type: "INTEGER", nullable: false),
                    message_thread_id = table.Column<int>(type: "INTEGER", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    detail_message_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_task_bindings", x => new { x.chat_id, x.message_thread_id, x.task_id });
                    table.ForeignKey(
                        name: "FK_telegram_task_bindings_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_task_bindings_task_id",
                table: "telegram_task_bindings",
                column: "task_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_task_bindings");
        }
    }
}
