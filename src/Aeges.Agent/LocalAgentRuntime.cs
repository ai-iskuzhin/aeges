using System.Security.Cryptography;
using System.Text;
using Aeges.Application;
using Aeges.Application.Artifacts;
using Aeges.Application.Iterations;
using Aeges.Application.RunnerDispatch;
using Aeges.Application.RunnerExecutions;
using Aeges.Application.Runtime;
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

        string? claimedTaskId = null;
        string? createdIterationId = null;
        string? promptArtifactId = null;
        string? promptPath = null;
        string? worktreePath = null;
        string? artifactOutputDirectory = null;
        string? runnerExecutionId = null;
        string? runnerStatus = null;
        int? runnerExitCode = null;
        string? runnerErrorSummary = null;
        var worktreeCreated = false;
        string? worktreeBaseCommit = null;

        if (options.ClaimQueuedTask)
        {
            var claim = await ClaimNextQueuedTaskAsync(unitOfWork, machineId, options, cancellationToken);
            claimedTaskId = claim.ClaimedTaskId;
            createdIterationId = claim.CreatedIterationId;
            promptArtifactId = claim.PromptArtifactId;
            promptPath = claim.PromptPath;
            worktreePath = claim.WorktreePath;
            artifactOutputDirectory = claim.ArtifactOutputDirectory;
            runnerExecutionId = claim.RunnerExecutionId;
            runnerStatus = claim.RunnerStatus;
            runnerExitCode = claim.RunnerExitCode;
            runnerErrorSummary = claim.RunnerErrorSummary;
            worktreeCreated = claim.WorktreeCreated;
            worktreeBaseCommit = claim.WorktreeBaseCommit;
        }

        var queuedTasks = await unitOfWork.Tasks.ListByStatusAsync(
            RuntimeTaskStatus.Queued,
            options.QueuedTaskPreviewLimit,
            cancellationToken);

        return new AgentRunSnapshot(
            machine.Id.Value,
            context.Database.GetDbConnection().DataSource,
            now,
            queuedTasks.Count,
            claimedTaskId,
            createdIterationId,
            promptArtifactId,
            promptPath,
            worktreePath,
            artifactOutputDirectory,
            runnerExecutionId,
            runnerStatus,
            runnerExitCode,
            runnerErrorSummary,
            worktreeCreated,
            worktreeBaseCommit);
    }

    private async Task<ClaimResult> ClaimNextQueuedTaskAsync(
        SqliteUnitOfWork unitOfWork,
        MachineId machineId,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        var queuedTasks = await unitOfWork.Tasks.ListByStatusAsync(
            RuntimeTaskStatus.Queued,
            options.QueuedTaskPreviewLimit,
            cancellationToken);
        var task = queuedTasks.FirstOrDefault(candidate => candidate.MachineId == machineId);

        if (task is null)
        {
            return ClaimResult.Empty;
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

        RunnerRunSummary? runnerRun = null;

        if (options.ExecuteRunner)
        {
            runnerRun = await ExecuteRunnerAsync(
                unitOfWork,
                task.Id,
                iteration.Value!.Id,
                prepared.RunnerRequest,
                options,
                cancellationToken);
        }

        return new ClaimResult(
            task.Id.Value,
            iteration.Value!.Id.Value,
            prepared.PromptArtifactId,
            prepared.PromptPath,
            prepared.WorktreePath,
            prepared.ArtifactOutputDirectory,
            runnerRun?.RunnerExecutionId,
            runnerRun?.Status,
            runnerRun?.ExitCode,
            runnerRun?.ErrorSummary,
            worktree is not null,
            worktree?.BaseCommit);
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

        var promptText = BuildPrompt(task, iteration);
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

        var result = await resolvedRunner.RunAsync(runnerRequest, cancellationToken);
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
                    ReasoningEffort: NormalizeOptional(options.CodexReasoningEffort)));
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

    private static string BuildPrompt(RuntimeTask task, TaskIteration iteration) =>
        $"""
        # Aeges Task Prompt

        Task ID: {task.Id}
        Iteration ID: {iteration.Id}
        Title: {task.Title}

        Goal:
        {task.Goal}

        Runtime instructions:
        - Follow the project rules and governance constraints.
        - Keep changes scoped to the task goal.
        - Produce durable outputs for review.
        """;

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

    private sealed record ClaimResult(
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
        public static ClaimResult Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, false, null);
    }
}
