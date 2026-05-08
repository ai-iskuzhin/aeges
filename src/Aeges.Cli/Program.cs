using System.Runtime.InteropServices;
using System.Text.Json;
using Aeges.Agent;
using Aeges.Application;
using Aeges.Application.Configuration;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.Runtime;
using Aeges.Application.Tasks;
using Aeges.Core;
using Aeges.Storage.Sqlite;
using Microsoft.EntityFrameworkCore;

using var cancellation = new CancellationTokenSource();

// Keep the CLI interruptible without letting Ctrl+C terminate in the middle of
// a storage update or agent heartbeat.
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await AegesCli.RunAsync(args, Console.Out, Console.Error, cancellation.Token);

internal static class AegesCli
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args is ["db", "status", .. var statusArgs])
        {
            return await RunDatabaseStatusAsync(statusArgs, output, error, cancellationToken);
        }

        if (args is ["db", "migrate", .. var migrateArgs])
        {
            return await RunDatabaseMigrateAsync(migrateArgs, output, error, cancellationToken);
        }

        if (args is ["task", "create", .. var createArgs])
        {
            return await RunTaskCreateAsync(createArgs, output, error, cancellationToken);
        }

        if (args is ["project", "add", .. var projectAddArgs])
        {
            return await RunProjectAddAsync(projectAddArgs, output, error, cancellationToken);
        }

        if (args is ["project", "list", .. var projectListArgs])
        {
            return await RunProjectListAsync(projectListArgs, output, error, cancellationToken);
        }

        if (args is ["machine", "add", .. var machineAddArgs])
        {
            return await RunMachineAddAsync(machineAddArgs, output, error, cancellationToken);
        }

        if (args is ["machine", "list", .. var machineListArgs])
        {
            return await RunMachineListAsync(machineListArgs, output, error, cancellationToken);
        }

        if (args is ["task", "status", .. var taskStatusArgs])
        {
            return await RunTaskStatusAsync(taskStatusArgs, output, error, cancellationToken);
        }

        if (args is ["agent", "run", .. var agentRunArgs])
        {
            return await RunAgentRunAsync(agentRunArgs, output, error, cancellationToken);
        }

        await WriteUsageAsync(error);

        return 2;
    }

    private static async Task<int> RunDatabaseStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var service = CreateMigrationService(options);
        var status = await service.GetStatusAsync(cancellationToken);
        await WriteStatusAsync(status, options.Json, output);

        return 0;
    }

    private static async Task<int> RunAgentRunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = AgentRunCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var runtime = new LocalAgentRuntime();
        var agentOptions = CreateAgentRunOptions(options);

        try
        {
            if (options.Once)
            {
                var snapshot = await runtime.RunOnceAsync(agentOptions, cancellationToken);
                await WriteAgentSnapshotAsync(snapshot, options.Json, output);
                return 0;
            }

            await output.WriteLineAsync($"Agent running for machine '{agentOptions.MachineId}'. Press Ctrl+C to stop.");

            // The initial shell deliberately performs bounded heartbeats only.
            // Task dispatch will be layered in later behind governed workflow checks.
            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = await runtime.RunOnceAsync(agentOptions, cancellationToken);
                await WriteAgentSnapshotAsync(snapshot, options.Json, output);
                await Task.Delay(options.PollInterval, cancellationToken);
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await output.WriteLineAsync("Agent stopped.");
            return 0;
        }
    }

    private static async Task<int> RunDatabaseMigrateAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var service = CreateMigrationService(options);
        var status = await service.MigrateAsync(cancellationToken);
        await WriteStatusAsync(status, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTaskCreateAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TaskCreateOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new TaskService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.CreateAsync(
            new CreateTaskRequest(
                new ProjectId(options.ProjectId!),
                new MachineId(options.MachineId!),
                options.Title!,
                options.Goal!,
                options.Priority,
                options.MaxIterations,
                options.TaskId is null ? null : new TaskId(options.TaskId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteTaskAsync(result.Value!, options.Json, output, "Created task");

        return 0;
    }

    private static async Task<int> RunProjectAddAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = ProjectAddOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.RegisterAsync(
            new RegisterProjectRequest(
                options.Name!,
                options.Path!,
                options.ProjectId is null ? null : new ProjectId(options.ProjectId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteProjectAsync(result.Value!, options.Json, output, "Added project");

        return 0;
    }

    private static async Task<int> RunProjectListAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectService(new SqliteUnitOfWork(context), new SystemClock());
        var projects = await service.ListAsync(cancellationToken);
        await WriteProjectsAsync(projects, options.Json, output);

        return 0;
    }

    private static async Task<int> RunMachineAddAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = MachineAddOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new MachineService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.RegisterAsync(
            new RegisterMachineRequest(
                options.Name!,
                options.Platform!,
                options.MachineId is null ? null : new MachineId(options.MachineId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteMachineAsync(result.Value!, options.Json, output, "Added machine");

        return 0;
    }

    private static async Task<int> RunMachineListAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new MachineService(new SqliteUnitOfWork(context), new SystemClock());
        var machines = await service.ListAsync(cancellationToken);
        await WriteMachinesAsync(machines, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTaskStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TaskStatusOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new TaskService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.GetAsync(new TaskId(options.TaskId!), cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteTaskAsync(result.Value!, options.Json, output, "Task");

        return 0;
    }

    private static SqliteMigrationService CreateMigrationService(CliOptions options)
    {
        return new SqliteMigrationService(ResolveConnectionString(options));
    }

    private static async Task<AegesDbContext> CreateReadyDbContextAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var context = new AegesDbContext(AegesDbContextOptions.Create(ResolveConnectionString(options)));
        await SqlitePragmas.ApplyAsync(context, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        return context;
    }

    private static string ResolveConnectionString(CliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return options.ConnectionString;
        }

        var layout = RuntimeDirectoryLayout.CreateDefault();
        var configuration = LoadConfiguration(options);

        var connectionString = configuration.Storage.ConnectionString;

        if (connectionString is null)
        {
            foreach (var directory in layout.RequiredDirectories)
            {
                Directory.CreateDirectory(directory);
            }

            connectionString = $"Data Source={layout.DatabasePath}";
        }

        return connectionString;
    }

    private static AegesConfiguration LoadConfiguration(CliOptions options) =>
        new AegesConfigurationLoader().Load(new AegesConfigurationLoaderOptions(options.ConfigPath));

    private static AgentRunOptions CreateAgentRunOptions(AgentRunCliOptions options)
    {
        var configuration = LoadConfiguration(options);
        var layout = RuntimeDirectoryLayout.CreateDefault();

        return new AgentRunOptions(
            ResolveConnectionString(options),
            options.MachineId ?? configuration.MachineId,
            options.MachineName ?? Environment.MachineName,
            options.Platform ?? RuntimeInformation.OSDescription,
            options.QueuePreviewLimit,
            options.RunnerId ?? configuration.Runners.Default,
            layout.RootPath,
            TimeSpan.FromSeconds(configuration.Runners.Codex.TimeoutSeconds),
            options.ClaimQueuedTask,
            options.ExecuteRunner,
            options.CreateWorktree,
            configuration.Runners.Codex.Executable,
            configuration.Runners.Codex.Model,
            configuration.Runners.Codex.ReasoningEffort);
    }

    private static async Task WriteStatusAsync(
        SqliteMigrationStatus status,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                status,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Database: {status.DatabasePath ?? "(unknown)"}");
        await output.WriteLineAsync($"Status: {(status.IsUpToDate ? "up-to-date" : "pending migrations")}");
        await output.WriteLineAsync($"Applied migrations: {status.AppliedMigrations.Count}");
        foreach (var migration in status.AppliedMigrations)
        {
            await output.WriteLineAsync($"  - {migration}");
        }

        await output.WriteLineAsync($"Pending migrations: {status.PendingMigrations.Count}");
        foreach (var migration in status.PendingMigrations)
        {
            await output.WriteLineAsync($"  - {migration}");
        }
    }

    private static async Task WriteTaskAsync(
        RuntimeTask task,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                TaskOutput.From(task),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {task.Id}");
        await output.WriteLineAsync($"Project: {task.ProjectId}");
        await output.WriteLineAsync($"Machine: {task.MachineId}");
        await output.WriteLineAsync($"Title: {task.Title}");
        await output.WriteLineAsync($"Status: {task.Status.ToStorageValue()}");
        await output.WriteLineAsync($"Priority: {task.Priority}");
        await output.WriteLineAsync($"Iterations: {task.CurrentIteration}/{task.MaxIterations}");
        await output.WriteLineAsync($"Created: {task.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {task.UpdatedAt:O}");

        if (!string.IsNullOrWhiteSpace(task.FailureReason))
        {
            await output.WriteLineAsync($"Failure: {task.FailureReason}");
        }
    }

    private static async Task WriteProjectAsync(
        RuntimeProject project,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                ProjectOutput.From(project),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {project.Id}");
        await output.WriteLineAsync($"Name: {project.Name}");
        await output.WriteLineAsync($"Path: {project.Path}");
        await output.WriteLineAsync($"Created: {project.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {project.UpdatedAt:O}");
    }

    private static async Task WriteProjectsAsync(
        IReadOnlyList<RuntimeProject> projects,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                projects.Select(ProjectOutput.From).ToArray(),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Projects: {projects.Count}");

        foreach (var project in projects)
        {
            await output.WriteLineAsync($"  - {project.Id} | {project.Name} | {project.Path}");
        }
    }

    private static async Task WriteMachineAsync(
        RuntimeMachine machine,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                MachineOutput.From(machine),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {machine.Id}");
        await output.WriteLineAsync($"Name: {machine.Name}");
        await output.WriteLineAsync($"Platform: {machine.Platform}");
        await output.WriteLineAsync($"Status: {machine.Status.ToStorageValue()}");
        await output.WriteLineAsync(
            $"Last seen: {(machine.LastSeenAt is null ? "(never)" : machine.LastSeenAt.Value.ToString("O"))}");
        await output.WriteLineAsync($"Created: {machine.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {machine.UpdatedAt:O}");
    }

    private static async Task WriteMachinesAsync(
        IReadOnlyList<RuntimeMachine> machines,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                machines.Select(MachineOutput.From).ToArray(),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Machines: {machines.Count}");

        foreach (var machine in machines)
        {
            await output.WriteLineAsync(
                $"  - {machine.Id} | {machine.Name} | {machine.Platform} | {machine.Status.ToStorageValue()}");
        }
    }

    private static async Task WriteAgentSnapshotAsync(
        AgentRunSnapshot snapshot,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                AgentSnapshotOutput.From(snapshot),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Agent heartbeat: {snapshot.MachineId}");
        await output.WriteLineAsync($"Database: {snapshot.DatabasePath ?? "(unknown)"}");
        await output.WriteLineAsync($"Queued tasks: {snapshot.QueuedTaskCount}");
        await output.WriteLineAsync($"Claimed task: {snapshot.ClaimedTaskId ?? "(none)"}");
        await output.WriteLineAsync($"Created iteration: {snapshot.CreatedIterationId ?? "(none)"}");
        await output.WriteLineAsync($"Prompt artifact: {snapshot.PromptArtifactId ?? "(none)"}");
        await output.WriteLineAsync($"Prompt path: {snapshot.PromptPath ?? "(none)"}");
        await output.WriteLineAsync($"Worktree path: {snapshot.WorktreePath ?? "(none)"}");
        await output.WriteLineAsync($"Artifact output: {snapshot.ArtifactOutputDirectory ?? "(none)"}");
        await output.WriteLineAsync($"Runner execution: {snapshot.RunnerExecutionId ?? "(none)"}");
        await output.WriteLineAsync($"Runner status: {snapshot.RunnerStatus ?? "(none)"}");
        await output.WriteLineAsync(
            $"Runner exit code: {(snapshot.RunnerExitCode is null ? "(none)" : snapshot.RunnerExitCode.Value.ToString())}");
        await output.WriteLineAsync($"Runner error: {snapshot.RunnerErrorSummary ?? "(none)"}");
        await output.WriteLineAsync($"Worktree created: {snapshot.WorktreeCreated}");
        await output.WriteLineAsync($"Worktree base commit: {snapshot.WorktreeBaseCommit ?? "(none)"}");
        await output.WriteLineAsync($"Heartbeat: {snapshot.HeartbeatAt:O}");
    }

    private static async Task WriteErrorAsync(
        ApplicationError errorValue,
        bool json,
        TextWriter error)
    {
        if (json)
        {
            await error.WriteLineAsync(JsonSerializer.Serialize(
                errorValue,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await error.WriteLineAsync($"{errorValue.Code}: {errorValue.Message}");
    }

    private static async Task WriteUsageAsync(TextWriter error)
    {
        await error.WriteLineAsync("Usage:");
        await error.WriteLineAsync("  aeges db status [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges db migrate [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges project add --name <name> --path <path> [--project-id <id>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges project list [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges machine add --name <name> --platform <text> [--machine-id <id>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges machine list [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges task create --project-id <id> --machine-id <id> --title <title> --goal <goal> [--task-id <id>] [--priority <int>] [--max-iterations <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges task status <task-id> [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges agent run [--once] [--machine-id <id>] [--machine-name <name>] [--platform <text>] [--runner-id <id>] [--no-claim] [--create-worktree] [--execute-runner] [--poll-interval-seconds <int>] [--queue-preview-limit <int>] [--config <path>] [--connection-string <value>] [--json]");
    }

    private class CliOptions
    {
        public string? ConfigPath { get; protected init; }

        public string? ConnectionString { get; protected init; }

        public bool Json { get; protected init; }

        public string? Error { get; protected init; }

        public static CliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return new CliOptions { Error = "--config requires a value." };
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return new CliOptions { Error = "--connection-string requires a value." };
                        }

                        break;
                    default:
                        return new CliOptions { Error = $"Unknown option '{args[index]}'." };
                }
            }

            return new CliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
            };
        }

        protected static bool TryReadValue(string[] args, ref int index, out string? value)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = null;
                return false;
            }

            index++;
            value = args[index];

            return true;
        }
    }

    private sealed class TaskCreateOptions : CliOptions
    {
        public string? ProjectId { get; private init; }

        public string? MachineId { get; private init; }

        public string? TaskId { get; private init; }

        public string? Title { get; private init; }

        public string? Goal { get; private init; }

        public int Priority { get; private init; }

        public int MaxIterations { get; private init; } = 3;

        public new static TaskCreateOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? projectId = null;
            string? machineId = null;
            string? taskId = null;
            string? title = null;
            string? goal = null;
            var priority = 0;
            var maxIterations = 3;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--project-id":
                        if (!TryReadValue(args, ref index, out projectId))
                        {
                            return ErrorResult("--project-id requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--task-id":
                        if (!TryReadValue(args, ref index, out taskId))
                        {
                            return ErrorResult("--task-id requires a value.");
                        }

                        break;
                    case "--title":
                        if (!TryReadValue(args, ref index, out title))
                        {
                            return ErrorResult("--title requires a value.");
                        }

                        break;
                    case "--goal":
                        if (!TryReadValue(args, ref index, out goal))
                        {
                            return ErrorResult("--goal requires a value.");
                        }

                        break;
                    case "--priority":
                        if (!TryReadInt(args, ref index, out priority))
                        {
                            return ErrorResult("--priority requires an integer value.");
                        }

                        break;
                    case "--max-iterations":
                        if (!TryReadInt(args, ref index, out maxIterations) || maxIterations <= 0)
                        {
                            return ErrorResult("--max-iterations requires an integer value greater than zero.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(projectId, "--project-id")
                ?? RequireText(machineId, "--machine-id")
                ?? RequireText(title, "--title")
                ?? RequireText(goal, "--goal")
                ?? new TaskCreateOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    ProjectId = projectId,
                    MachineId = machineId,
                    TaskId = taskId,
                    Title = title,
                    Goal = goal,
                    Priority = priority,
                    MaxIterations = maxIterations,
                };
        }

        private static bool TryReadInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text))
            {
                value = 0;
                return false;
            }

            return int.TryParse(text, out value);
        }

        private static TaskCreateOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static TaskCreateOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class ProjectAddOptions : CliOptions
    {
        public string? ProjectId { get; private init; }

        public string? Name { get; private init; }

        public string? Path { get; private init; }

        public new static ProjectAddOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? projectId = null;
            string? name = null;
            string? path = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--project-id":
                        if (!TryReadValue(args, ref index, out projectId))
                        {
                            return ErrorResult("--project-id requires a value.");
                        }

                        break;
                    case "--name":
                        if (!TryReadValue(args, ref index, out name))
                        {
                            return ErrorResult("--name requires a value.");
                        }

                        break;
                    case "--path":
                        if (!TryReadValue(args, ref index, out path))
                        {
                            return ErrorResult("--path requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(name, "--name")
                ?? RequireText(path, "--path")
                ?? new ProjectAddOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    ProjectId = projectId,
                    Name = name,
                    Path = path,
                };
        }

        private static ProjectAddOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static ProjectAddOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class MachineAddOptions : CliOptions
    {
        public string? MachineId { get; private init; }

        public string? Name { get; private init; }

        public string? Platform { get; private init; }

        public new static MachineAddOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? machineId = null;
            string? name = null;
            string? platform = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--name":
                        if (!TryReadValue(args, ref index, out name))
                        {
                            return ErrorResult("--name requires a value.");
                        }

                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref index, out platform))
                        {
                            return ErrorResult("--platform requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(name, "--name")
                ?? RequireText(platform, "--platform")
                ?? new MachineAddOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    MachineId = machineId,
                    Name = name,
                    Platform = platform,
                };
        }

        private static MachineAddOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static MachineAddOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class TaskStatusOptions : CliOptions
    {
        public string? TaskId { get; private init; }

        public new static TaskStatusOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? taskId = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--task-id":
                        if (!TryReadValue(args, ref index, out taskId))
                        {
                            return ErrorResult("--task-id requires a value.");
                        }

                        break;
                    default:
                        if (args[index].StartsWith("--", StringComparison.Ordinal))
                        {
                            return ErrorResult($"Unknown option '{args[index]}'.");
                        }

                        if (taskId is not null)
                        {
                            return ErrorResult("Only one task identifier can be supplied.");
                        }

                        taskId = args[index];
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ErrorResult("Task identifier is required.");
            }

            return new TaskStatusOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                TaskId = taskId,
            };
        }

        private static TaskStatusOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class AgentRunCliOptions : CliOptions
    {
        public bool Once { get; private init; }

        public string? MachineId { get; private init; }

        public string? MachineName { get; private init; }

        public string? Platform { get; private init; }

        public TimeSpan PollInterval { get; private init; } = TimeSpan.FromSeconds(5);

        public int QueuePreviewLimit { get; private init; } = 100;

        public string? RunnerId { get; private init; }

        public bool ClaimQueuedTask { get; private init; } = true;

        public bool ExecuteRunner { get; private init; }

        public bool CreateWorktree { get; private init; }

        public new static AgentRunCliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? machineId = null;
            string? machineName = null;
            string? platform = null;
            string? runnerId = null;
            var once = false;
            var json = false;
            var pollInterval = TimeSpan.FromSeconds(5);
            var queuePreviewLimit = 100;
            var claimQueuedTask = true;
            var executeRunner = false;
            var createWorktree = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--once":
                        once = true;
                        break;
                    case "--json":
                        json = true;
                        break;
                    case "--no-claim":
                        claimQueuedTask = false;
                        break;
                    case "--execute-runner":
                        executeRunner = true;
                        break;
                    case "--create-worktree":
                        createWorktree = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--machine-name":
                        if (!TryReadValue(args, ref index, out machineName))
                        {
                            return ErrorResult("--machine-name requires a value.");
                        }

                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref index, out platform))
                        {
                            return ErrorResult("--platform requires a value.");
                        }

                        break;
                    case "--runner-id":
                        if (!TryReadValue(args, ref index, out runnerId))
                        {
                            return ErrorResult("--runner-id requires a value.");
                        }

                        break;
                    case "--poll-interval-seconds":
                        if (!TryReadPositiveInt(args, ref index, out var seconds))
                        {
                            return ErrorResult("--poll-interval-seconds requires an integer value greater than zero.");
                        }

                        pollInterval = TimeSpan.FromSeconds(seconds);
                        break;
                    case "--queue-preview-limit":
                        if (!TryReadPositiveInt(args, ref index, out queuePreviewLimit))
                        {
                            return ErrorResult("--queue-preview-limit requires an integer value greater than zero.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new AgentRunCliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                Once = once,
                MachineId = machineId,
                MachineName = machineName,
                Platform = platform,
                PollInterval = pollInterval,
                QueuePreviewLimit = queuePreviewLimit,
                RunnerId = runnerId,
                ClaimQueuedTask = claimQueuedTask,
                ExecuteRunner = executeRunner,
                CreateWorktree = createWorktree,
            };
        }

        private static bool TryReadPositiveInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text) || !int.TryParse(text, out value))
            {
                value = 0;
                return false;
            }

            return value > 0;
        }

        private static AgentRunCliOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed record ProjectOutput(
        string Id,
        string Name,
        string Path,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static ProjectOutput From(RuntimeProject project) =>
            new(
                project.Id.Value,
                project.Name,
                project.Path,
                project.CreatedAt,
                project.UpdatedAt);
    }

    private sealed record MachineOutput(
        string Id,
        string Name,
        string Platform,
        string Status,
        DateTimeOffset? LastSeenAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static MachineOutput From(RuntimeMachine machine) =>
            new(
                machine.Id.Value,
                machine.Name,
                machine.Platform,
                machine.Status.ToStorageValue(),
                machine.LastSeenAt,
                machine.CreatedAt,
                machine.UpdatedAt);
    }

    private sealed record TaskOutput(
        string Id,
        string ProjectId,
        string MachineId,
        string Title,
        string Goal,
        string Status,
        int Priority,
        int MaxIterations,
        int CurrentIteration,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        DateTimeOffset? CancelledAt,
        string? FailureReason)
    {
        public static TaskOutput From(RuntimeTask task) =>
            new(
                task.Id.Value,
                task.ProjectId.Value,
                task.MachineId.Value,
                task.Title,
                task.Goal,
                task.Status.ToStorageValue(),
                task.Priority,
                task.MaxIterations,
                task.CurrentIteration,
                task.CreatedAt,
                task.UpdatedAt,
                task.StartedAt,
                task.CompletedAt,
                task.CancelledAt,
                task.FailureReason);
    }

    private sealed record AgentSnapshotOutput(
        string MachineId,
        string? DatabasePath,
        DateTimeOffset HeartbeatAt,
        int QueuedTaskCount,
        string? ClaimedTaskId,
        string? CreatedIterationId,
        string? PromptArtifactId,
        string? PromptPath,
        string? WorktreePath,
        string? ArtifactOutputDirectory,
        string? RunnerExecutionId,
        string? RunnerStatus,
        int? RunnerExitCode,
        string? RunnerErrorSummary,
        bool WorktreeCreated,
        string? WorktreeBaseCommit)
    {
        public static AgentSnapshotOutput From(AgentRunSnapshot snapshot) =>
            new(
                snapshot.MachineId,
                snapshot.DatabasePath,
                snapshot.HeartbeatAt,
                snapshot.QueuedTaskCount,
                snapshot.ClaimedTaskId,
                snapshot.CreatedIterationId,
                snapshot.PromptArtifactId,
                snapshot.PromptPath,
                snapshot.WorktreePath,
                snapshot.ArtifactOutputDirectory,
                snapshot.RunnerExecutionId,
                snapshot.RunnerStatus,
                snapshot.RunnerExitCode,
                snapshot.RunnerErrorSummary,
                snapshot.WorktreeCreated,
                snapshot.WorktreeBaseCommit);
    }
}
