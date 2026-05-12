using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aeges.Storage.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectRootsAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "group_id",
                table: "projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_groups",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_roots",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_roots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projects_group_id",
                table: "projects",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_groups_is_archived",
                table: "project_groups",
                column: "is_archived");

            migrationBuilder.CreateIndex(
                name: "IX_project_groups_name",
                table: "project_groups",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_groups_path",
                table: "project_groups",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_roots_is_archived",
                table: "project_roots",
                column: "is_archived");

            migrationBuilder.CreateIndex(
                name: "IX_project_roots_name",
                table: "project_roots",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_roots_path",
                table: "project_roots",
                column: "path",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_projects_project_groups_group_id",
                table: "projects",
                column: "group_id",
                principalTable: "project_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_projects_project_groups_group_id",
                table: "projects");

            migrationBuilder.DropTable(
                name: "project_groups");

            migrationBuilder.DropTable(
                name: "project_roots");

            migrationBuilder.DropIndex(
                name: "IX_projects_group_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "projects");
        }
    }
}
