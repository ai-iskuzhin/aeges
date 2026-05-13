using Aeges.Core;
using Aeges.Storage.Sqlite;
using Aeges.Storage.Sqlite.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Agent.Tests;

public sealed class LocalAgentRuntimeTests
{
    [Fact]
    public async Task RunOnce_registers_machine_and_returns_queue_snapshot()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");

        try
        {
            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    $"Data Source={databasePath}",
                    "machine-001",
                    "local-test",
                    "test-platform"),
                CancellationToken.None);

            Assert.Equal("machine-001", snapshot.MachineId);
            Assert.EndsWith(Path.GetFileName(databasePath), snapshot.DatabasePath, StringComparison.Ordinal);
            Assert.Equal(clock.Now, snapshot.HeartbeatAt);
            Assert.Equal(0, snapshot.QueuedTaskCount);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create($"Data Source={databasePath}"));
            var machine = await new SqliteMachineRepository(context)
                .GetByIdAsync(new MachineId("machine-001"), CancellationToken.None);

            Assert.NotNull(machine);
            Assert.Equal(MachineStatus.Online, machine.Status);
            Assert.Equal(clock.Now, machine.LastSeenAt);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_updates_existing_machine_heartbeat()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var options = new AgentRunOptions(
            $"Data Source={databasePath}",
            "machine-001",
            "local-test",
            "test-platform");

        try
        {
            await runtime.RunOnceAsync(options, CancellationToken.None);
            clock.Now = clock.Now.AddMinutes(1);

            var snapshot = await runtime.RunOnceAsync(options, CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create($"Data Source={databasePath}"));
            var machines = await new SqliteMachineRepository(context).ListAsync(CancellationToken.None);

            Assert.Single(machines);
            Assert.Equal(clock.Now, snapshot.HeartbeatAt);
            Assert.Equal(clock.Now, machines[0].LastSeenAt);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_claims_one_queued_task_assigned_to_machine()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"aeges-runtime-{Guid.NewGuid():N}");

        try
        {
            await SeedProjectMachineAndTaskAsync(connectionString, clock.Now, new MachineId("machine-001"));

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    RunnerId: "mock",
                    RuntimeRootPath: runtimeRoot),
                CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
            var unitOfWork = new SqliteUnitOfWork(context);
            var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);
            var iterations = await unitOfWork.Iterations.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);
            var artifacts = await unitOfWork.Artifacts.ListByIterationAsync(new IterationId(snapshot.CreatedIterationId!), CancellationToken.None);

            Assert.Equal("task-001", snapshot.ClaimedTaskId);
            Assert.NotNull(snapshot.CreatedIterationId);
            Assert.NotNull(snapshot.PromptArtifactId);
            Assert.NotNull(snapshot.PromptPath);
            Assert.NotNull(snapshot.WorktreePath);
            Assert.NotNull(snapshot.ArtifactOutputDirectory);
            Assert.Equal(0, snapshot.QueuedTaskCount);
            Assert.NotNull(task);
            Assert.Equal(RuntimeTaskStatus.Planning, task.Status);
            Assert.Equal(1, task.CurrentIteration);
            var iteration = Assert.Single(iterations);
            Assert.Equal(new RunnerId("mock"), iteration.RunnerId);
            Assert.Equal(1, iteration.IterationNumber);
            Assert.Equal(new IterationId(snapshot.CreatedIterationId), iteration.Id);
            Assert.Equal(new ArtifactId(snapshot.PromptArtifactId), iteration.PromptArtifactId);
            Assert.Equal(snapshot.WorktreePath, iteration.WorktreePath);
            Assert.Equal(
                Path.Combine(runtimeRoot, "worktrees", "project-001", "task-001", snapshot.CreatedIterationId),
                snapshot.WorktreePath);
            Assert.Equal(
                Path.Combine(runtimeRoot, "artifacts", "project-001", "task-001", snapshot.CreatedIterationId),
                snapshot.ArtifactOutputDirectory);
            Assert.Equal(Path.Combine(snapshot.ArtifactOutputDirectory!, "prompt.md"), snapshot.PromptPath);
            Assert.True(File.Exists(snapshot.PromptPath));
            Assert.Contains("Goal:", await File.ReadAllTextAsync(snapshot.PromptPath));

            var promptArtifact = Assert.Single(artifacts);
            Assert.Equal(new ArtifactId(snapshot.PromptArtifactId), promptArtifact.Id);
            Assert.Equal(ArtifactType.Prompt, promptArtifact.Type);
            Assert.Equal($"project-001/task-001/{snapshot.CreatedIterationId}/prompt.md", promptArtifact.RelativePath);
            Assert.True(promptArtifact.SizeBytes > 0);
            Assert.NotNull(promptArtifact.Sha256);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
            DeleteDirectoryIfExists(runtimeRoot);
        }
    }

    [Fact]
    public async Task RunOnce_can_claim_parallel_tasks_from_different_projects()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"aeges-runtime-{Guid.NewGuid():N}");

        try
        {
            await SeedProjectsMachineAndTasksAsync(
                connectionString,
                clock.Now,
                new MachineId("machine-001"),
                [
                    new SeedTask("project-001", "Aeges", "/work/aeges", "task-001"),
                    new SeedTask("project-002", "Website", "/work/site", "task-002"),
                ]);

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    MaxParallelTasks: 2,
                    RunnerId: "mock",
                    RuntimeRootPath: runtimeRoot),
                CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
            var unitOfWork = new SqliteUnitOfWork(context);
            var firstTask = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);
            var secondTask = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-002"), CancellationToken.None);

            Assert.Equal(2, snapshot.ClaimedTasks.Count);
            Assert.Contains(snapshot.ClaimedTasks, task => task.TaskId == "task-001" && task.ProjectId == "project-001");
            Assert.Contains(snapshot.ClaimedTasks, task => task.TaskId == "task-002" && task.ProjectId == "project-002");
            Assert.Equal("task-001", snapshot.ClaimedTaskId);
            Assert.Equal(0, snapshot.QueuedTaskCount);
            Assert.NotNull(firstTask);
            Assert.NotNull(secondTask);
            Assert.Equal(RuntimeTaskStatus.Planning, firstTask.Status);
            Assert.Equal(RuntimeTaskStatus.Planning, secondTask.Status);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
            DeleteDirectoryIfExists(runtimeRoot);
        }
    }

    [Fact]
    public async Task RunOnce_serializes_queued_tasks_from_the_same_project()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"aeges-runtime-{Guid.NewGuid():N}");

        try
        {
            await SeedProjectsMachineAndTasksAsync(
                connectionString,
                clock.Now,
                new MachineId("machine-001"),
                [
                    new SeedTask("project-001", "Aeges", "/work/aeges", "task-001"),
                    new SeedTask("project-001", "Aeges", "/work/aeges", "task-002"),
                ]);

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    MaxParallelTasks: 2,
                    RunnerId: "mock",
                    RuntimeRootPath: runtimeRoot),
                CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
            var unitOfWork = new SqliteUnitOfWork(context);
            var firstTask = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);
            var secondTask = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-002"), CancellationToken.None);

            var claimedTask = Assert.Single(snapshot.ClaimedTasks);
            Assert.Equal("task-001", claimedTask.TaskId);
            Assert.Equal(1, snapshot.QueuedTaskCount);
            Assert.NotNull(firstTask);
            Assert.NotNull(secondTask);
            Assert.Equal(RuntimeTaskStatus.Planning, firstTask.Status);
            Assert.Equal(RuntimeTaskStatus.Queued, secondTask.Status);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
            DeleteDirectoryIfExists(runtimeRoot);
        }
    }

    [Fact]
    public async Task RunOnce_does_not_claim_task_assigned_to_another_machine()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await SeedProjectMachineAndTaskAsync(connectionString, clock.Now, new MachineId("other-machine"));

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform"),
                CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
            var unitOfWork = new SqliteUnitOfWork(context);
            var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);
            var iterations = await unitOfWork.Iterations.ListByTaskAsync(new TaskId("task-001"), CancellationToken.None);

            Assert.Null(snapshot.ClaimedTaskId);
            Assert.Null(snapshot.CreatedIterationId);
            Assert.Equal(1, snapshot.QueuedTaskCount);
            Assert.NotNull(task);
            Assert.Equal(RuntimeTaskStatus.Queued, task.Status);
            Assert.Empty(iterations);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_can_skip_task_claiming()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await SeedProjectMachineAndTaskAsync(connectionString, clock.Now, new MachineId("machine-001"));

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    ClaimQueuedTask: false),
                CancellationToken.None);

            Assert.Null(snapshot.ClaimedTaskId);
            Assert.Null(snapshot.CreatedIterationId);
            Assert.Equal(1, snapshot.QueuedTaskCount);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_can_execute_mock_runner_and_record_result()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"aeges-runtime-{Guid.NewGuid():N}");

        try
        {
            await SeedProjectMachineAndTaskAsync(connectionString, clock.Now, new MachineId("machine-001"));

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    RunnerId: "mock",
                    RuntimeRootPath: runtimeRoot,
                    ExecuteRunner: true),
                CancellationToken.None);

            await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
            var unitOfWork = new SqliteUnitOfWork(context);
            var task = await unitOfWork.Tasks.GetByIdAsync(new TaskId("task-001"), CancellationToken.None);
            var iteration = await unitOfWork.Iterations.GetByIdAsync(new IterationId(snapshot.CreatedIterationId!), CancellationToken.None);
            var executions = await unitOfWork.RunnerExecutions.ListByIterationAsync(
                new IterationId(snapshot.CreatedIterationId!),
                CancellationToken.None);
            var artifacts = await unitOfWork.Artifacts.ListByIterationAsync(
                new IterationId(snapshot.CreatedIterationId!),
                CancellationToken.None);
            var events = await unitOfWork.RuntimeEvents.ListByTaskAsync(new TaskId("task-001"), 10, CancellationToken.None);

            Assert.NotNull(snapshot.RunnerExecutionId);
            Assert.Equal("succeeded", snapshot.RunnerStatus);
            Assert.Equal(0, snapshot.RunnerExitCode);
            Assert.Null(snapshot.RunnerErrorSummary);
            Assert.NotNull(task);
            Assert.Equal(RuntimeTaskStatus.Reviewing, task.Status);
            Assert.NotNull(iteration);
            Assert.Equal(TaskIterationStatus.Completed, iteration.Status);
            var execution = Assert.Single(executions);
            Assert.Equal(new RunnerExecutionId(snapshot.RunnerExecutionId), execution.Id);
            Assert.Equal(0, execution.ExitCode);
            Assert.True(execution.IsCompleted);
            Assert.False(execution.TimedOut);
            Assert.False(execution.Cancelled);
            Assert.Contains(artifacts, artifact => artifact.Type == ArtifactType.StdoutLog);
            Assert.Contains(artifacts, artifact => artifact.Type == ArtifactType.StderrLog);
            Assert.Contains(artifacts, artifact => artifact.Type == ArtifactType.Result);
            Assert.Equal(artifacts.Single(artifact => artifact.Type == ArtifactType.Result).Id, iteration.ResultArtifactId);
            Assert.Contains(events, runtimeEvent => runtimeEvent.EventType == "runner.started");
            Assert.Contains(events, runtimeEvent => runtimeEvent.EventType == "runner.message");
            Assert.Contains(events, runtimeEvent => runtimeEvent.EventType == "runner.completed");
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
            DeleteDirectoryIfExists(runtimeRoot);
        }
    }

    [Fact]
    public async Task RunOnce_can_create_git_worktree_for_claimed_iteration()
    {
        using var repository = await TemporaryGitRepository.CreateAsync();
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero));
        var runtime = new LocalAgentRuntime(clock);
        var databasePath = Path.Combine(Path.GetTempPath(), $"aeges-agent-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var runtimeRoot = Path.Combine(repository.RootPath, "runtime");

        try
        {
            await SeedProjectMachineAndTaskAsync(
                connectionString,
                clock.Now,
                new MachineId("machine-001"),
                repository.Path);

            var snapshot = await runtime.RunOnceAsync(
                new AgentRunOptions(
                    connectionString,
                    "machine-001",
                    "local-test",
                    "test-platform",
                    RunnerId: "mock",
                    RuntimeRootPath: runtimeRoot,
                    CreateWorktree: true),
                CancellationToken.None);

            Assert.True(snapshot.WorktreeCreated);
            Assert.Equal(repository.HeadCommit, snapshot.WorktreeBaseCommit);
            Assert.NotNull(snapshot.WorktreePath);
            Assert.True(Directory.Exists(snapshot.WorktreePath));
            Assert.True(File.Exists(Path.Combine(snapshot.WorktreePath, "file.txt")));
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    [Fact]
    public async Task RunOnce_rejects_codex_execution_without_worktree_creation()
    {
        var runtime = new LocalAgentRuntime(new FixedClock(new DateTimeOffset(2026, 05, 08, 08, 00, 00, TimeSpan.Zero)));
        var options = new AgentRunOptions(
            "Data Source=/tmp/aeges-agent-validation.db",
            "machine-001",
            "local-test",
            "test-platform",
            ExecuteRunner: true);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => runtime.RunOnceAsync(options, CancellationToken.None));

        Assert.Contains("Codex runner execution requires worktree creation", exception.Message);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task SeedProjectMachineAndTaskAsync(
        string connectionString,
        DateTimeOffset now,
        MachineId taskMachineId,
        string projectPath = "/work/aeges")
    {
        await SeedProjectsMachineAndTasksAsync(
            connectionString,
            now,
            taskMachineId,
            [new SeedTask("project-001", "Aeges", projectPath, "task-001")]);
    }

    private static async Task SeedProjectsMachineAndTasksAsync(
        string connectionString,
        DateTimeOffset now,
        MachineId taskMachineId,
        IReadOnlyList<SeedTask> tasks)
    {
        await using var context = new AegesDbContext(AegesDbContextOptions.Create(connectionString));
        await SqlitePragmas.ApplyAsync(context, CancellationToken.None);
        await context.Database.MigrateAsync(CancellationToken.None);
        var unitOfWork = new SqliteUnitOfWork(context);

        foreach (var project in tasks
            .GroupBy(task => task.ProjectId)
            .Select(group => group.First()))
        {
            await unitOfWork.Projects.AddAsync(
                RuntimeProject.Create(
                    new ProjectId(project.ProjectId),
                    project.ProjectName,
                    project.ProjectPath,
                    now),
                CancellationToken.None);
        }

        await unitOfWork.Machines.AddAsync(
            RuntimeMachine.Create(taskMachineId, $"machine-{taskMachineId.Value}", "test-platform", now),
            CancellationToken.None);

        foreach (var task in tasks)
        {
            await unitOfWork.Tasks.AddAsync(
                RuntimeTask.Create(
                    new TaskId(task.TaskId),
                    new ProjectId(task.ProjectId),
                    taskMachineId,
                    "Queued task",
                    "Do governed work.",
                    now),
                CancellationToken.None);
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private sealed record SeedTask(
        string ProjectId,
        string ProjectName,
        string ProjectPath,
        string TaskId);

    private sealed class TemporaryGitRepository : IDisposable
    {
        private TemporaryGitRepository(string rootPath, string repositoryPath, string headCommit)
        {
            RootPath = rootPath;
            Path = repositoryPath;
            HeadCommit = headCommit;
        }

        public string RootPath { get; }

        public string Path { get; }

        public string HeadCommit { get; }

        public static async Task<TemporaryGitRepository> CreateAsync()
        {
            var rootPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aeges-agent-git-{Guid.NewGuid():N}");
            var repositoryPath = System.IO.Path.Combine(rootPath, "repo");
            Directory.CreateDirectory(repositoryPath);

            await RunGitAsync(repositoryPath, ["init", "-b", "main"]);
            await RunGitAsync(repositoryPath, ["config", "user.email", "aeges@example.test"]);
            await RunGitAsync(repositoryPath, ["config", "user.name", "Aeges Tests"]);
            await File.WriteAllTextAsync(System.IO.Path.Combine(repositoryPath, "file.txt"), "initial\n");
            await RunGitAsync(repositoryPath, ["add", "file.txt"]);
            await RunGitAsync(repositoryPath, ["commit", "-m", "Initial commit"]);
            var headCommit = (await RunGitAsync(repositoryPath, ["rev-parse", "HEAD"])).Trim();

            return new TemporaryGitRepository(rootPath, repositoryPath, headCommit);
        }

        public void Dispose()
        {
            DeleteDirectoryIfExists(RootPath);
        }

        private static async Task<string> RunGitAsync(string workingDirectory, IReadOnlyList<string> arguments)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Git process failed to start.");
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Git failed with exit code {process.ExitCode}: {stderr}");
            }

            return stdout;
        }
    }
}
