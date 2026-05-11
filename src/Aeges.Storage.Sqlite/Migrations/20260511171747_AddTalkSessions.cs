using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aeges.Storage.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTalkSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "talk_sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    runner_id = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    external_session_id = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talk_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "talk_messages",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    session_id = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talk_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_talk_messages_talk_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "talk_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_talk_messages_created_at",
                table: "talk_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_talk_messages_session_id",
                table: "talk_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_talk_sessions_source",
                table: "talk_sessions",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "IX_talk_sessions_status",
                table: "talk_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_talk_sessions_updated_at",
                table: "talk_sessions",
                column: "updated_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "talk_messages");

            migrationBuilder.DropTable(
                name: "talk_sessions");
        }
    }
}
