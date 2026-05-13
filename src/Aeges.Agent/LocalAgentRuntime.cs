using System.Security.Cryptography;
using System.Text;
using Aeges.Application;
using Aeges.Application.Artifacts;
using Aeges.Application.Iterations;
using Aeges.Application.RunnerDispatch;
using Aeges.Application.RunnerExecutions;
using Aeges.Application.Runtime;
using Aeges.Application.RuntimeEvents;
using Aeges.Application.Tasks;
using Aeges.Core;
using Aeges.Git;
using Aeges.Runners;
using Aeges.Runners.Codex;
using Aeges.Storage.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aeges.Agent;

/// <summary>
/// Runs the local Aeges agent shell against the configured SQLite runtime state.
/// </summary>
public sealed class LocalAgentRuntime
{
    private const int MaxReviewFeedbackBytes = 32 * 1024;
    private readonly IClock clock;
    private readonly IAegesRunner? runner;
    private readonly IGitRuntime gitRuntime;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalAgentRuntime"/> class.
    /// </summary>
    /// <param name="clock">The deterministic runtime clock.</param>
    /// <param name="runner">The optional runner used for opt-in execution.</param>
    /// <param name="gitRuntime">The Git runtime used for opt-in worktree creation.</param>
    public LocalAgentRuntime(IClock? clock = null, IAegesRunner? runner = null, IGitRuntime? gitRuntime = null)
    {
        this.clock = clock ?? new SystemClock();
        this.runner = runner;
        this.gitRuntime = gitRuntime ?? new ProcessGitRuntime();
    }

    /// <summary>
    /// Initializes local runtime storage, records a machine heartbeat, claims queued work, and returns a queue snapshot.
    /// </summary>
    /// <param name="options">The local agent run options.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The resulting agent heartbeat snapshot.</returns>
    public async Task<AgentRunSnapshot> RunOnceAsync(
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        Validate(options);

        await using var context = new AegesDbContext(AegesDbContextOptions.Create(options.ConnectionString));
        await SqlitePragmas.ApplyAsync(context, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        var unitOfWork = new SqliteUnitOfWork(context);
        var now = clock.Now;
        var machineId = new MachineId(options.MachineId);
        var machine = await unitOfWork.Machines.GetByIdAsync(machineId, cancellationToken);

        if (machine is null)
        {
            machine = RuntimeMachine.Create(machineId, options.MachineName, options.Platform, now);
            machine.MarkOnline(now);
            await unitOfWork.Machines.AddAsync(machine, cancellationToken);
        }
        else
        {
            machine.MarkOnline(now);
            await unitOfWork.Machines.UpdateAsync(machine, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var claimedTasks = new List<PreparedClaim>();

        if (options.ClaimQueuedTask)
        {
            var claimedProjectIds = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < options.MaxParallelTasks; index++)
            {
                var claim = await ClaimNextQueuedTaskAsync(
                    unitOfWork,
                    machineId,
                    options,
                    claimedProjectIds,
                    cancellationToken);

                if (claim is null)
                {
                    break;
                }

                claimedTasks.Add(claim);
                claimedProjectIds.Add(claim.ProjectId.Value);
            }

            if (options.ExecuteRunner && claimedTasks.Count > 0)
            {
                var executedClaims = await Task.WhenAll(
                    claimedTasks.Select(claim => ExecutePreparedClaimAsync(claim, options, cancellationToken)));
                claimedTasks = executedClaims.ToList();
            }
        }

        var queuedTasks = await unitOfWork.Tasks.ListByStatusAsync(
            RuntimeTaskStatus.Queued,
            options.QueuedTaskPreviewLimit,
            cancellationToken);
        var taskSnapshots = claimedTasks.Select(claim => claim.Snapshot).ToArray();
        var firstTask = taskSnapshots.FirstOrDefault();

        return new AgentRunSnapshot(
            machine.Id.Value,
            context.Database.GetDbConnection().DataSource,
            now,
            queuedTasks.Count,
            firstTask?.TaskId,
            firstTask?.CreatedIterationId,
            firstTask?.PromptArtifactId,
            firstTask?.PromptPath,
            firstTask?.WorktreePath,
            firstTask?.ArtifactOutputDirectory,
            firstTask?.RunnerExecutionId,
            firstTask?.RunnerStatus,
            firstTask?.RunnerExitCode,
            firstTask?.RunnerErrorSummary,
            firstTask?.WorktreeCreated ?? false,
            firstTask?.WorktreeBaseCommit,
            taskSnapshots);
    }

    /// <summary>
    /// Marks tasks left in active runner states by a previous agent process as failed.
    /// </summary>
    /// <param name="options">The local agent run options.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The number of recovered orphaned tasks.</returns>
    public async Task<int> RecoverOrphanedRunningTasksAsync(
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        Validate(options);

        await using var context = new AegesDbContext(AegesDbContextOptions.Create(options.ConnectionString));
        await SqlitePragmas.ApplyAsync(context, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        var unitOfWork = new SqliteUnitOfWork(context);
        var machineId = new MachineId(options.MachineId);
        var orphanedTasks = new List<RuntimeTask>();

        foreach (var status in new[] { RuntimeTaskStatus.Planning, RuntimeTaskStatus.Running })
        {
            var tasks = await unitOfWork.Tasks.ListByStatusAsync(status, int.MaxValue, cancellationToken);
            orphanedTasks.AddRange(tasks.Where(task => task.MachineId == machineId));
        }

        if (orphanedTasks.Count == 0)
        {
            return 0;
        }

        var taskService = new TaskService(unitOfWork, clock);
        var iterationService = new TaskIterationService(unitOfWork, clock);
        var executionService = new RunnerExecutionService(unitOfWork, clock);
        var runtimeEventService = new RuntimeEventService(unitOfWork, clock);
        var recovered = 0;

        foreach (var task in orphanedTasks)
        {
            var reason = "Agent process restarted before the task completed; previous runner execution was orphaned.";
            var iterations = await iterationService.ListByTaskAsync(task.Id, cancellationToken);
            var executions = await executionService.ListByTaskAsync(task.Id, cancellationToken);

            foreach (var execution in executions.Where(execution => !execution.IsCompleted))
            {
                await RequireSuccessAsync(executionService.RecordCancellationAsync(execution.Id, cancellationToken));
            }

            foreach (var iteration in iterations.Where(iteration => !iteration.Status.IsTerminal()))
            {
                if (iteration.Status == TaskIterationStatus.Created)
                {
                    await RequireSuccessAsync(iterationService.CancelAsync(iteration.Id, cancellationToken));
                }
                else
                {
                    await RequireSuccessAsync(iterationService.FailAsync(iteration.Id, reason, cancellationToken));
                }
            }

            await RequireSuccessAsync(taskService.FailAsync(task.Id, reason, cancellationToken));
            await RequireSuccessAsync(runtimeEventService.RecordAsync(
                new RecordRuntimeEventRequest(
                    task.Id,
                    IterationId: null,
                    MachineId: machineId,
                    "agent.orphaned_task_recovered",
                    reason),
                cancellationToken));
            recovered++;
        }

        return recovered;
    }

    private async Task<PreparedClaim?> ClaimNextQueuedTaskAsync(
        SqliteUnitOfWork unitOfWork,
        MachineId machineId,
        AgentRunOptions options,
        ISet<string> claimedProjectIds,
        CancellationToken cancellationToken)
    {
        var activeProjectIds = await ListActiveProjectIdsAsync(unitOfWork, cancellationToken);
        var queuedTasks = await unitOfWork.Tasks.ListByStatusAsync(
            RuntimeTaskStatus.Queued,
            options.QueuedTaskPreviewLimit,
            cancellationToken);
        var task = queuedTasks.FirstOrDefault(candidate =>
            candidate.MachineId == machineId
            && !activeProjectIds.Contains(candidate.ProjectId.Value)
            && !claimedProjectIds.Contains(candidate.ProjectId.Value));

        if (task is null)
        {
            return null;
        }

        var taskService = new TaskService(unitOfWork, clock);
        var iterationService = new TaskIterationService(unitOfWork, clock);
        var planning = await taskService.StartPlanningAsync(task.Id, cancellationToken);

        if (!planning.IsSuccess)
        {
            throw new InvalidOperationException(planning.Error!.Message);
        }

        var iteration = await iterationService.CreateNextAsync(
            new CreateTaskIterationRequest(task.Id, new RunnerId(options.RunnerId)),
            cancellationToken);

        if (!iteration.IsSuccess)
        {
            throw new InvalidOperationException(iteration.Error!.Message);
        }

        var prepared = await PrepareRunnerBundleAsync(unitOfWork, task, iteration.Value!, options, cancellationToken);
        GitWorktreeInfo? worktree = null;

        if (options.CreateWorktree)
        {
            worktree = await CreateWorktreeAsync(task, prepared.Project, iteration.Value!, prepared.RunnerRequest, cancellationToken);
        }

        var snapshot = new AgentTaskRunSnapshot(
            task.Id.Value,
            task.ProjectId.Value,
            iteration.Value!.Id.Value,
            prepared.PromptArtifactId,
            prepared.PromptPath,
            prepared.WorktreePath,
            prepared.ArtifactOutputDirectory,
            null,
            null,
            null,
            null,
            worktree is not null,
            worktree?.BaseCommit);

        return new PreparedClaim(task.Id, task.ProjectId, iteration.Value.Id, prepared.RunnerRequest, snapshot);
    }

    private async Task<PreparedClaim> ExecutePreparedClaimAsync(
        PreparedClaim claim,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        await using var context = new AegesDbContext(AegesDbContextOptions.Create(options.ConnectionString));
        await SqlitePragmas.ApplyAsync(context, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(context);
        var runnerRun = await ExecuteRunnerAsync(
            unitOfWork,
            claim.TaskId,
            claim.IterationId,
            claim.RunnerRequest,
            options,
            cancellationToken);
        var snapshot = claim.Snapshot with
        {
            RunnerExecutionId = runnerRun.RunnerExecutionId,
            RunnerStatus = runnerRun.Status,
            RunnerExitCode = runnerRun.ExitCode,
            RunnerErrorSummary = runnerRun.ErrorSummary,
        };

        return claim with { Snapshot = snapshot };
    }

    private static async Task<HashSet<string>> ListActiveProjectIdsAsync(
        SqliteUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var projectIds = new HashSet<string>(StringComparer.Ordinal);
        var activeStatuses = new[]
        {
            RuntimeTaskStatus.Planning,
            RuntimeTaskStatus.Running,
            RuntimeTaskStatus.Reviewing,
            RuntimeTaskStatus.WaitingApproval,
        };

        foreach (var status in activeStatuses)
        {
            var tasks = await unitOfWork.Tasks.ListByStatusAsync(status, int.MaxValue, cancellationToken);

            foreach (var task in tasks)
            {
                projectIds.Add(task.ProjectId.Value);
            }
        }

        return projectIds;
    }

    private async Task<PreparedRunnerBundle> PrepareRunnerBundleAsync(
        SqliteUnitOfWork unitOfWork,
        RuntimeTask task,
        TaskIteration iteration,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        var project = await unitOfWork.Projects.GetByIdAsync(task.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project '{task.ProjectId}' was not found.");
        var layout = CreateRuntimeLayout(options);
        var runnerRequest = new RunnerRequestFactory().Create(
            new CreateRunnerRequestRequest(
                task,
                project,
                iteration,
                layout,
                options.RunnerTimeout ?? TimeSpan.FromMinutes(30)));

        if (!runnerRequest.IsSuccess)
        {
            throw new InvalidOperationException(runnerRequest.Error!.Message);
        }

        Directory.CreateDirectory(runnerRequest.Value!.ArtifactOutputDirectory);

        var reviewFeedback = await ReadReviewFeedbackAsync(unitOfWork, layout, task.Id, cancellationToken);
        var promptText = BuildPrompt(task, iteration, reviewFeedback);
        var promptBytes = Encoding.UTF8.GetBytes(promptText);
        await File.WriteAllTextAsync(runnerRequest.Value.PromptPath, promptText, Encoding.UTF8, cancellationToken);

        iteration.AssignWorktree(runnerRequest.Value.WorktreePath, clock.Now);
        await unitOfWork.Iterations.UpdateAsync(iteration, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var relativePromptPath = ToArtifactRelativePath(layout, runnerRequest.Value.PromptPath);
        var promptArtifact = await new ArtifactService(unitOfWork, clock).RegisterAsync(
            new RegisterArtifactRequest(
                task.Id,
                iteration.Id,
                ArtifactType.Prompt,
                relativePromptPath,
                promptBytes.LongLength,
                Convert.ToHexString(SHA256.HashData(promptBytes)).ToLowerInvariant()),
            cancellationToken);

        if (!promptArtifact.IsSuccess)
        {
            throw new InvalidOperationException(promptArtifact.Error!.Message);
        }

        return new PreparedRunnerBundle(
            promptArtifact.Value!.Id.Value,
            runnerRequest.Value.PromptPath,
            runnerRequest.Value.WorktreePath,
            runnerRequest.Value.ArtifactOutputDirectory,
            runnerRequest.Value,
            project);
    }

    private async Task<GitWorktreeInfo> CreateWorktreeAsync(
        RuntimeTask task,
        RuntimeProject project,
        TaskIteration iteration,
        RunnerRequest runnerRequest,
        CancellationToken cancellationToken)
    {
        var baseCommit = await gitRuntime.GetBaseCommitAsync(
            project.Path,
            project.Id,
            task.Id,
            iteration.Id,
            cancellationToken);
        var request = new GitWorktreeRequest(
            project.Path,
            Path.GetFullPath(Path.Combine(runnerRequest.WorktreePath, "..", "..", "..")),
            project.Id,
            task.Id,
            iteration.Id,
            CreateBranchName(task.Id, iteration.Id),
            baseCommit.CommitSha);

        return await gitRuntime.CreateWorktreeAsync(request, cancellationToken);
    }

    private async Task<RunnerRunSummary> ExecuteRunnerAsync(
        SqliteUnitOfWork unitOfWork,
        TaskId taskId,
        IterationId iterationId,
        RunnerRequest runnerRequest,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        var resolvedRunner = ResolveRunner(options);
        var taskService = new TaskService(unitOfWork, clock);
        var iterationService = new TaskIterationService(unitOfWork, clock);
        await RequireSuccessAsync(taskService.StartRunningAsync(taskId, cancellationToken));
        await RequireSuccessAsync(iterationService.StartRunningAsync(iterationId, cancellationToken));

        var executionService = new RunnerExecutionService(unitOfWork, clock);
        var execution = await executionService.StartAsync(
            new StartRunnerExecutionRequest(
                taskId,
                iterationId,
                resolvedRunner.Id,
                $"runner:{resolvedRunner.Id.Value}",
                runnerRequest.WorktreePath),
            cancellationToken);
        await RequireSuccessAsync(execution);

        var progressSink = new RuntimeEventRunnerProgressSink(
            unitOfWork,
            clock,
            taskId,
            iterationId,
            new MachineId(options.MachineId));
        var result = await resolvedRunner.RunAsync(runnerRequest, progressSink, cancellationToken);
        await progressSink.ReportAsync(
            new RunnerProgressEvent(
                "runner.completed",
                $"Runner '{resolvedRunner.Id.Value}' completed with status '{result.Status.ToStorageValue()}'."),
            cancellationToken);
        await CompleteRunnerExecutionAsync(executionService, execution.Value!.Id, result, cancellationToken);
        await RegisterRunnerArtifactsAsync(unitOfWork, taskId, iterationId, runnerRequest, options, result, cancellationToken);
        await ApplyRunnerResultAsync(taskService, iterationService, taskId, iterationId, result, cancellationToken);

        return new RunnerRunSummary(
            execution.Value.Id.Value,
            result.Status.ToStorageValue(),
            result.ExitCode,
            result.ErrorSummary);
    }

    private async Task RegisterRunnerArtifactsAsync(
        SqliteUnitOfWork unitOfWork,
        TaskId taskId,
        IterationId iterationId,
        RunnerRequest runnerRequest,
        AgentRunOptions options,
        RunnerResult result,
        CancellationToken cancellationToken)
    {
        var artifactService = new ArtifactService(unitOfWork, clock);
        var layout = CreateRuntimeLayout(options);
        var artifactTypes = new Dictionary<string, ArtifactType>(StringComparer.Ordinal);

        AddPath(artifactTypes, result.StdoutPath, ArtifactType.StdoutLog);
        AddPath(artifactTypes, result.StderrPath, ArtifactType.StderrLog);
        AddPath(artifactTypes, result.ResultArtifactPath, ArtifactType.Result);

        foreach (var path in result.ProducedArtifactPaths)
        {
            AddPath(artifactTypes, path, ArtifactType.Metadata);
        }

        foreach (var pair in artifactTypes)
        {
            if (!File.Exists(pair.Key))
            {
                continue;
            }

            var fileInfo = new FileInfo(pair.Key);
            await using var stream = File.OpenRead(pair.Key);
            var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            var registered = await artifactService.RegisterAsync(
                new RegisterArtifactRequest(
                    taskId,
                    iterationId,
                    pair.Value,
                    ToArtifactRelativePath(layout, pair.Key),
                    fileInfo.Length,
                    sha256),
                cancellationToken);

            await RequireSuccessAsync(registered);
        }
    }

    private static void AddPath(
        IDictionary<string, ArtifactType> artifacts,
        string? path,
        ArtifactType type)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);

        if (!artifacts.ContainsKey(fullPath))
        {
            artifacts.Add(fullPath, type);
        }
    }

    private IAegesRunner ResolveRunner(AgentRunOptions options)
    {
        if (runner is not null)
        {
            if (runner.Id != new RunnerId(options.RunnerId))
            {
                throw new InvalidOperationException(
                    $"Configured runner '{options.RunnerId}' does not match injected runner '{runner.Id}'.");
            }

            return runner;
        }

        if (options.RunnerId.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            return new MockAegesRunner(new MockRunnerOptions(new RunnerId(options.RunnerId)));
        }

        if (options.RunnerId.Equals("codex", StringComparison.OrdinalIgnoreCase))
        {
            return new CodexRunner(
                new CodexRunnerOptions(
                    options.CodexExecutable,
                    Model: NormalizeOptional(options.CodexModel),
                    ReasoningEffort: NormalizeOptional(options.CodexReasoningEffort),
                    SandboxMode: options.CodexSandboxMode,
                    BypassApprovalsAndSandbox: options.CodexBypassApprovalsAndSandbox));
        }

        throw new InvalidOperationException(
            $"Runner '{options.RunnerId}' is not available unless a runner is provided by the host.");
    }

    private static async Task CompleteRunnerExecutionAsync(
        RunnerExecutionService executionService,
        RunnerExecutionId executionId,
        RunnerResult result,
        CancellationToken cancellationToken)
    {
        var completion = result.Status switch
        {
            RunnerStatus.TimedOut => await executionService.RecordTimeoutAsync(executionId, cancellationToken),
            RunnerStatus.Cancelled => await executionService.RecordCancellationAsync(executionId, cancellationToken),
            _ => await executionService.RecordExitAsync(executionId, result.ExitCode ?? DefaultExitCode(result.Status), cancellationToken),
        };

        await RequireSuccessAsync(completion);
    }

    private static async Task ApplyRunnerResultAsync(
        TaskService taskService,
        TaskIterationService iterationService,
        TaskId taskId,
        IterationId iterationId,
        RunnerResult result,
        CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case RunnerStatus.Succeeded:
                await RequireSuccessAsync(iterationService.StartReviewAsync(iterationId, cancellationToken));
                await RequireSuccessAsync(iterationService.CompleteAsync(iterationId, cancellationToken));
                await RequireSuccessAsync(taskService.StartReviewAsync(taskId, cancellationToken));
                break;
            case RunnerStatus.ApprovalRequired:
                await RequireSuccessAsync(iterationService.WaitForApprovalAsync(iterationId, cancellationToken));
                await RequireSuccessAsync(taskService.WaitForApprovalAsync(taskId, cancellationToken));
                break;
            case RunnerStatus.Cancelled:
                await RequireSuccessAsync(iterationService.CancelAsync(iterationId, cancellationToken));
                await RequireSuccessAsync(taskService.CancelAsync(taskId, cancellationToken));
                break;
            case RunnerStatus.Failed:
            case RunnerStatus.TimedOut:
                var reason = result.ErrorSummary ?? $"Runner ended with status {result.Status.ToStorageValue()}.";
                await RequireSuccessAsync(iterationService.FailAsync(iterationId, reason, cancellationToken));
                await RequireSuccessAsync(taskService.FailAsync(taskId, reason, cancellationToken));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown runner status.");
        }
    }

    private static int DefaultExitCode(RunnerStatus status) =>
        status is RunnerStatus.Succeeded or RunnerStatus.ApprovalRequired ? 0 : 1;

    private static async Task RequireSuccessAsync<TValue>(Task<ApplicationResult<TValue>> resultTask) =>
        await RequireSuccessAsync(await resultTask);

    private static Task RequireSuccessAsync<TValue>(ApplicationResult<TValue> result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return Task.CompletedTask;
    }

    private static void Validate(AgentRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("Connection string must not be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.MachineId))
        {
            throw new ArgumentException("Machine ID must not be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.MachineName))
        {
            throw new ArgumentException("Machine name must not be empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Platform))
        {
            throw new ArgumentException("Platform must not be empty.", nameof(options));
        }

        if (options.QueuedTaskPreviewLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.QueuedTaskPreviewLimit,
                "Queued task preview limit must be greater than zero.");
        }

        if (options.MaxParallelTasks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxParallelTasks,
                "Maximum parallel tasks must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.RunnerId))
        {
            throw new ArgumentException("Runner ID must not be empty.", nameof(options));
        }

        if (options.RuntimeRootPath is not null && string.IsNullOrWhiteSpace(options.RuntimeRootPath))
        {
            throw new ArgumentException("Runtime root path must not be empty.", nameof(options));
        }

        if (options.RunnerTimeout is { } runnerTimeout && runnerTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.RunnerTimeout,
                "Runner timeout must be greater than zero.");
        }

        if (options.ExecuteRunner && !options.ClaimQueuedTask)
        {
            throw new ArgumentException("Runner execution requires task claiming to be enabled.", nameof(options));
        }

        if (options.ExecuteRunner
            && options.RunnerId.Equals("codex", StringComparison.OrdinalIgnoreCase)
            && !options.CreateWorktree)
        {
            throw new ArgumentException("Codex runner execution requires worktree creation to be enabled.", nameof(options));
        }

        if (options.CreateWorktree && !options.ClaimQueuedTask)
        {
            throw new ArgumentException("Worktree creation requires task claiming to be enabled.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.CodexExecutable))
        {
            throw new ArgumentException("Codex executable must not be empty.", nameof(options));
        }

        if (options.CodexModel is not null && string.IsNullOrWhiteSpace(options.CodexModel))
        {
            throw new ArgumentException("Codex model must not be empty when configured.", nameof(options));
        }

        if (options.CodexReasoningEffort is not null && string.IsNullOrWhiteSpace(options.CodexReasoningEffort))
        {
            throw new ArgumentException("Codex reasoning effort must not be empty when configured.", nameof(options));
        }
    }

    private static RuntimeDirectoryLayout CreateRuntimeLayout(AgentRunOptions options) =>
        options.RuntimeRootPath is null
            ? RuntimeDirectoryLayout.CreateDefault()
            : RuntimeDirectoryLayout.Create(options.RuntimeRootPath);

    private static string BuildPrompt(
        RuntimeTask task,
        TaskIteration iteration,
        string reviewFeedback) =>
        $"""
        # Aeges Task Prompt

        Task ID: {task.Id}
        Iteration ID: {iteration.Id}
        Title: {task.Title}

        Goal:
        {task.Goal}

        Review feedback:
        {reviewFeedback}

        Runtime instructions:
        - Follow the project rules and governance constraints.
        - Keep changes scoped to the task goal.
        - Produce durable outputs for review.
        """;

    private static async Task<string> ReadReviewFeedbackAsync(
        SqliteUnitOfWork unitOfWork,
        RuntimeDirectoryLayout layout,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var artifacts = await unitOfWork.Artifacts.ListByTaskAsync(taskId, cancellationToken);
        var reviewArtifacts = artifacts
            .Where(artifact => artifact.Type == ArtifactType.Review)
            .OrderBy(artifact => artifact.CreatedAt)
            .ToArray();

        if (reviewArtifacts.Length == 0)
        {
            return "(none)";
        }

        var sections = new List<string>();

        foreach (var artifact in reviewArtifacts)
        {
            var fullPath = ResolveArtifactPath(layout, artifact.RelativePath);

            if (!File.Exists(fullPath))
            {
                sections.Add($"- Missing review artifact: {artifact.RelativePath}");
                continue;
            }

            var content = await ReadBoundedTextAsync(fullPath, cancellationToken);
            sections.Add(
                $"""
                ## Review artifact {artifact.Id}
                {content}
                """);
        }

        return string.Join("\n\n", sections);
    }

    private static string ResolveArtifactPath(RuntimeDirectoryLayout layout, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(layout.ArtifactsPath, relativePath));
        var artifactRoot = Path.GetFullPath(layout.ArtifactsPath);

        if (!fullPath.StartsWith(artifactRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Review artifact path escaped the configured artifact root.");
        }

        return fullPath;
    }

    private static async Task<string> ReadBoundedTextAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var length = (int)Math.Min(stream.Length, MaxReviewFeedbackBytes);
        var buffer = new byte[length];
        var read = await stream.ReadAsync(buffer.AsMemory(0, length), cancellationToken);
        var content = Encoding.UTF8.GetString(buffer, 0, read);

        return stream.Length > MaxReviewFeedbackBytes
            ? content + "\n[truncated]"
            : content;
    }

    private static string ToArtifactRelativePath(RuntimeDirectoryLayout layout, string artifactPath)
    {
        var relativePath = Path.GetRelativePath(layout.ArtifactsPath, artifactPath);

        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Prepared artifact path escaped the configured artifact root.");
        }

        return relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string CreateBranchName(TaskId taskId, IterationId iterationId) =>
        $"aeges/{taskId.Value}/{iterationId.Value}";

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record PreparedRunnerBundle(
        string PromptArtifactId,
        string PromptPath,
        string WorktreePath,
        string ArtifactOutputDirectory,
        RunnerRequest RunnerRequest,
        RuntimeProject Project);

    private sealed record RunnerRunSummary(
        string RunnerExecutionId,
        string Status,
        int? ExitCode,
        string? ErrorSummary);

    private sealed class RuntimeEventRunnerProgressSink : IRunnerProgressSink
    {
        private readonly RuntimeEventService service;
        private readonly TaskId taskId;
        private readonly IterationId iterationId;
        private readonly MachineId machineId;

        public RuntimeEventRunnerProgressSink(
            SqliteUnitOfWork unitOfWork,
            IClock clock,
            TaskId taskId,
            IterationId iterationId,
            MachineId machineId)
        {
            service = new RuntimeEventService(unitOfWork, clock);
            this.taskId = taskId;
            this.iterationId = iterationId;
            this.machineId = machineId;
        }

        public async Task ReportAsync(RunnerProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            var result = await service.RecordAsync(
                new RecordRuntimeEventRequest(
                    taskId,
                    iterationId,
                    machineId,
                    progressEvent.EventType,
                    progressEvent.Message,
                    progressEvent.PayloadJson),
                cancellationToken);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.Error!.Message);
            }
        }
    }

    private sealed record PreparedClaim(
        TaskId TaskId,
        ProjectId ProjectId,
        IterationId IterationId,
        RunnerRequest RunnerRequest,
        AgentTaskRunSnapshot Snapshot);
}
