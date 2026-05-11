using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aeges.Storage.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportCallbackActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transport_callback_actions",
                columns: table => new
                {
                    token = table.Column<string>(type: "TEXT", nullable: false),
                    transport = table.Column<string>(type: "TEXT", nullable: false),
                    scope = table.Column<string>(type: "TEXT", nullable: false),
                    action_type = table.Column<string>(type: "TEXT", nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    use_count = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transport_callback_actions", x => new { x.transport, x.token });
                });

            migrationBuilder.CreateIndex(
                name: "IX_transport_callback_actions_expires_at",
                table: "transport_callback_actions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_transport_callback_actions_transport_scope",
                table: "transport_callback_actions",
                columns: new[] { "transport", "scope" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transport_callback_actions");
        }
    }
}
