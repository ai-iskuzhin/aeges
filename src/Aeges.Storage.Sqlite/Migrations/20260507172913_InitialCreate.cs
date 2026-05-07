using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aeges.Storage.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "machines",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    platform = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<string>(type: "TEXT", nullable: false),
                    machine_id = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    goal = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    max_iterations = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    current_iteration = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    failure_reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_tasks_machines_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locks",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<string>(type: "TEXT", nullable: false),
                    path_pattern = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locks", x => x.id);
                    table.ForeignKey(
                        name: "FK_locks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_locks_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_iterations",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    iteration_number = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    runner_id = table.Column<string>(type: "TEXT", nullable: false),
                    worktree_path = table.Column<string>(type: "TEXT", nullable: true),
                    prompt_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    result_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    diff_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    failure_reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_iterations", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_iterations_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approvals",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    iteration_id = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: false),
                    requested_action = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    resolved_by = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approvals", x => x.id);
                    table.ForeignKey(
                        name: "FK_approvals_task_iterations_iteration_id",
                        column: x => x.iteration_id,
                        principalTable: "task_iterations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_approvals_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artifacts",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    iteration_id = table.Column<string>(type: "TEXT", nullable: true),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    relative_path = table.Column<string>(type: "TEXT", nullable: false),
                    size_bytes = table.Column<long>(type: "INTEGER", nullable: true),
                    sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_artifacts_task_iterations_iteration_id",
                        column: x => x.iteration_id,
                        principalTable: "task_iterations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_artifacts_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "runner_executions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    iteration_id = table.Column<string>(type: "TEXT", nullable: false),
                    runner_id = table.Column<string>(type: "TEXT", nullable: false),
                    command = table.Column<string>(type: "TEXT", nullable: false),
                    working_directory = table.Column<string>(type: "TEXT", nullable: false),
                    exit_code = table.Column<int>(type: "INTEGER", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    timed_out = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    cancelled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runner_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_runner_executions_task_iterations_iteration_id",
                        column: x => x.iteration_id,
                        principalTable: "task_iterations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_runner_executions_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "runtime_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: true),
                    iteration_id = table.Column<string>(type: "TEXT", nullable: true),
                    machine_id = table.Column<string>(type: "TEXT", nullable: true),
                    event_type = table.Column<string>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runtime_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_runtime_events_machines_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_runtime_events_task_iterations_iteration_id",
                        column: x => x.iteration_id,
                        principalTable: "task_iterations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_runtime_events_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approvals_iteration_id",
                table: "approvals",
                column: "iteration_id");

            migrationBuilder.CreateIndex(
                name: "IX_approvals_status",
                table: "approvals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_approvals_task_id",
                table: "approvals",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_iteration_id",
                table: "artifacts",
                column: "iteration_id");

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_task_id",
                table: "artifacts",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_type",
                table: "artifacts",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "IX_locks_project_id",
                table: "locks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_locks_released_at",
                table: "locks",
                column: "released_at");

            migrationBuilder.CreateIndex(
                name: "IX_locks_task_id",
                table: "locks",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_machines_status",
                table: "machines",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_projects_path",
                table: "projects",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_runner_executions_iteration_id",
                table: "runner_executions",
                column: "iteration_id");

            migrationBuilder.CreateIndex(
                name: "IX_runner_executions_runner_id",
                table: "runner_executions",
                column: "runner_id");

            migrationBuilder.CreateIndex(
                name: "IX_runner_executions_task_id",
                table: "runner_executions",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_runtime_events_created_at",
                table: "runtime_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_runtime_events_event_type",
                table: "runtime_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_runtime_events_iteration_id",
                table: "runtime_events",
                column: "iteration_id");

            migrationBuilder.CreateIndex(
                name: "IX_runtime_events_machine_id",
                table: "runtime_events",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_runtime_events_task_id",
                table: "runtime_events",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_iterations_status",
                table: "task_iterations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_task_iterations_task_id",
                table: "task_iterations",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_iterations_task_id_iteration_number",
                table: "task_iterations",
                columns: new[] { "task_id", "iteration_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_machine_id",
                table: "tasks",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_project_id",
                table: "tasks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_status",
                table: "tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_status_priority_created_at",
                table: "tasks",
                columns: new[] { "status", "priority", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approvals");

            migrationBuilder.DropTable(
                name: "artifacts");

            migrationBuilder.DropTable(
                name: "locks");

            migrationBuilder.DropTable(
                name: "runner_executions");

            migrationBuilder.DropTable(
                name: "runtime_events");

            migrationBuilder.DropTable(
                name: "task_iterations");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "machines");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
