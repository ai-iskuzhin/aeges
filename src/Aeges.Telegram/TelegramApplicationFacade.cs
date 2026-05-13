using Aeges.Application;
using Aeges.Application.Approvals;
using Aeges.Application.Artifacts;
using Aeges.Application.Configuration;
using Aeges.Application.Iterations;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.RunnerExecutions;
using Aeges.Application.Runtime;
using Aeges.Application.RuntimeEvents;
using Aeges.Application.Talk;
using Aeges.Application.TelegramTaskBindings;
using Aeges.Application.TelegramUsers;
using Aeges.Application.Tasks;
using Aeges.Core;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aeges.Telegram;

/// <summary>
/// Application-service backed implementation of <see cref="ITelegramApplicationFacade"/>.
/// </summary>
public sealed class TelegramApplicationFacade : ITelegramApplicationFacade
{
    private readonly ProjectService projectService;
    private readonly ProjectGroupService projectGroupService;
    private readonly MachineService machineService;
    private readonly TaskService taskService;
    private readonly TaskIterationService iterationService;
    private readonly ArtifactService artifactService;
    private readonly RunnerExecutionService runnerExecutionService;
    private readonly RuntimeEventService runtimeEventService;
    private readonly ApprovalService approvalService;
    private readonly TalkService talkService;
    private readonly TelegramUserService telegramUserService;
    private readonly TelegramTaskBindingService telegramTaskBindingService;
    private readonly RuntimeDirectoryLayout runtimeLayout;
    private readonly AegesConfiguration configuration;
    private readonly string configPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramApplicationFacade"/> class.
    /// </summary>
    /// <param name="projectService">The project application service.</param>
    /// <param name="projectGroupService">The project group application service.</param>
    /// <param name="machineService">The machine application service.</param>
    /// <param name="taskService">The task application service.</param>
    /// <param name="iterationService">The task iteration application service.</param>
    /// <param name="artifactService">The artifact application service.</param>
    /// <param name="runnerExecutionService">The runner execution application service.</param>
    /// <param name="runtimeEventService">The runtime event application service.</param>
    /// <param name="approvalService">The approval application service.</param>
    /// <param name="talkService">The governed talk application service.</param>
    /// <param name="telegramUserService">The Telegram user application service.</param>
    /// <param name="telegramTaskBindingService">The Telegram task binding application service.</param>
    /// <param name="configuration">The loaded local runtime configuration.</param>
    /// <param name="configPath">The configuration file path to update for settings changes.</param>
    /// <param name="runtimeLayout">The runtime directory layout used to resolve local artifact previews.</param>
    public TelegramApplicationFacade(
        ProjectService projectService,
        ProjectGroupService projectGroupService,
        MachineService machineService,
        TaskService taskService,
        TaskIterationService iterationService,
        ArtifactService artifactService,
        RunnerExecutionService runnerExecutionService,
        RuntimeEventService runtimeEventService,
        ApprovalService approvalService,
        TalkService talkService,
        TelegramUserService telegramUserService,
        TelegramTaskBindingService telegramTaskBindingService,
        AegesConfiguration configuration,
        string configPath,
        RuntimeDirectoryLayout? runtimeLayout = null)
    {
        this.projectService = projectService;
        this.projectGroupService = projectGroupService;
        this.machineService = machineService;
        this.taskService = taskService;
        this.iterationService = iterationService;
        this.artifactService = artifactService;
        this.runnerExecutionService = runnerExecutionService;
        this.runtimeEventService = runtimeEventService;
        this.approvalService = approvalService;
        this.talkService = talkService;
        this.telegramUserService = telegramUserService;
        this.telegramTaskBindingService = telegramTaskBindingService;
        this.configuration = configuration;
        this.configPath = configPath;
        this.runtimeLayout = runtimeLayout ?? RuntimeDirectoryLayout.CreateDefault();
    }

    /// <inheritdoc />
    public async Task<TelegramUserAuthorization> EnsureTelegramUserAsync(
        long chatId,
        RuntimeTelegramUserProfile profile,
        CancellationToken cancellationToken) =>
        await telegramUserService.EnsureAsync(chatId, profile, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTelegramUser>> ListTelegramUsersAsync(CancellationToken cancellationToken) =>
        await telegramUserService.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramUserAccessSnapshot>> GetTelegramUserAccessAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken) =>
        await telegramUserService.GetAccessAsync(userId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTelegramUser>> ApproveTelegramUserAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken) =>
        await telegramUserService.ApproveAsync(userId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTelegramUser>> DenyTelegramUserAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken) =>
        await telegramUserService.DenyAsync(userId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramUserAccessSnapshot>> SetTelegramProjectAccessAsync(
        TelegramUserId userId,
        ProjectId projectId,
        bool allowed,
        CancellationToken cancellationToken) =>
        await telegramUserService.SetProjectAccessAsync(userId, projectId, allowed, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramUserAccessSnapshot>> SetTelegramProjectGroupAccessAsync(
        TelegramUserId userId,
        ProjectGroupId projectGroupId,
        bool allowed,
        CancellationToken cancellationToken) =>
        await telegramUserService.SetProjectGroupAccessAsync(userId, projectGroupId, allowed, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken) =>
        await projectService.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProjectGroup>> ListProjectGroupsAsync(CancellationToken cancellationToken) =>
        await projectGroupService.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProject>> ListActiveProjectsAsync(CancellationToken cancellationToken) =>
        await projectService.ListActiveAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeProject>> GetProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken) =>
        await projectService.GetAsync(projectId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeProject>> ArchiveProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken) =>
        await projectService.ArchiveAsync(projectId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<TalkExchange>> SendTalkMessageAsync(
        string source,
        string message,
        CancellationToken cancellationToken) =>
        await talkService.SendAsync(
            new SendTalkMessageRequest(source, message),
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken) =>
        await machineService.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await taskService.ListByStatusAsync(RuntimeTaskStatus.Queued, limit, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTask>> ListTasksByStatusAsync(
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken) =>
        await taskService.ListByStatusAsync(status, limit, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTask>> ListProjectTasksByStatusAsync(
        ProjectId projectId,
        RuntimeTaskStatus status,
        int limit,
        CancellationToken cancellationToken)
    {
        var tasks = await taskService.ListByProjectAsync(projectId, cancellationToken);

        return [.. tasks.Where(task => task.Status == status).Take(limit)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingApprovalsAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await approvalService.ListPendingAsync(limit, cancellationToken);

    /// <inheritdoc />
    public Task<TelegramRunnerSettings> GetRunnerSettingsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(CreateRunnerSettings());

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramRunnerSettings>> SetCodexSandboxModeAsync(
        string sandboxMode,
        CancellationToken cancellationToken)
    {
        if (sandboxMode is not ("workspace-write" or "danger-full-access"))
        {
            return ApplicationResult<TelegramRunnerSettings>.Failure(
                "invalid_sandbox_mode",
                "Telegram settings can switch Codex sandbox mode between 'workspace-write' and 'danger-full-access'.");
        }

        configuration.Runners.Codex.SandboxMode = sandboxMode;
        await SaveConfigurationAsync(cancellationToken);

        return ApplicationResult<TelegramRunnerSettings>.Success(CreateRunnerSettings());
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramRunnerSettings>> SetCodexBypassApprovalsAndSandboxAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        configuration.Runners.Codex.BypassApprovalsAndSandbox = enabled;
        await SaveConfigurationAsync(cancellationToken);

        return ApplicationResult<TelegramRunnerSettings>.Success(CreateRunnerSettings());
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramRunnerSettings>> SetAgentMaxParallelTasksAsync(
        int maxParallelTasks,
        CancellationToken cancellationToken)
    {
        if (maxParallelTasks is not (1 or 2 or 4 or 8 or 16 or 32))
        {
            return ApplicationResult<TelegramRunnerSettings>.Failure(
                "invalid_parallel_task_limit",
                "Telegram settings can set parallel tasks to 1, 2, 4, 8, 16, or 32.");
        }

        configuration.Agent.MaxParallelTasks = maxParallelTasks;
        await SaveConfigurationAsync(cancellationToken);

        return ApplicationResult<TelegramRunnerSettings>.Success(CreateRunnerSettings());
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramAgentRestartResult>> RestartAgentAsync(CancellationToken cancellationToken)
    {
        return await RunAgentProcessCommandAsync("restart", "agent_restart", "Agent restarted.", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramAgentRestartResult>> StartAgentAsync(CancellationToken cancellationToken)
    {
        return await RunAgentProcessCommandAsync("start", "agent_start", "Agent started.", cancellationToken);
    }

    private async Task<ApplicationResult<TelegramAgentRestartResult>> RunAgentProcessCommandAsync(
        string command,
        string errorCodePrefix,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            var startInfo = new ProcessStartInfo(ResolveExecutablePath())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("agent");
            startInfo.ArgumentList.Add(command);
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(configPath);

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return ApplicationResult<TelegramAgentRestartResult>.Failure(
                    $"{errorCodePrefix}_failed",
                    $"Failed to start the aeges agent {command} command.");
            }

            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);

            await process.WaitForExitAsync(timeout.Token);
            var outputText = (await stdout).Trim();
            var errorText = (await stderr).Trim();

            if (process.ExitCode != 0)
            {
                return ApplicationResult<TelegramAgentRestartResult>.Failure(
                    $"{errorCodePrefix}_failed",
                    FirstNonEmpty(errorText, outputText, $"aeges agent {command} exited with code {process.ExitCode}."));
            }

            return ApplicationResult<TelegramAgentRestartResult>.Success(
                new TelegramAgentRestartResult(FirstNonEmpty(outputText, fallbackMessage)));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApplicationResult<TelegramAgentRestartResult>.Failure(
                $"{errorCodePrefix}_timeout",
                $"aeges agent {command} did not finish within 30 seconds.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return ApplicationResult<TelegramAgentRestartResult>.Failure(
                $"{errorCodePrefix}_unavailable",
                $"Could not run aeges agent {command}: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTask>> CreateTaskAsync(
        ProjectId projectId,
        MachineId machineId,
        string title,
        string goal,
        CancellationToken cancellationToken) =>
        await taskService.CreateAsync(
            new CreateTaskRequest(projectId, machineId, title, goal),
            cancellationToken);

    /// <inheritdoc />
    public async Task RecordTaskBindingAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        int? detailMessageId,
        CancellationToken cancellationToken) =>
        await telegramTaskBindingService.RecordAsync(
            chatId,
            messageThreadId,
            taskId,
            detailMessageId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeTelegramTaskBinding>> ListTaskBindingsAsync(CancellationToken cancellationToken) =>
        await telegramTaskBindingService.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ForgetTaskBindingAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await telegramTaskBindingService.ForgetAsync(chatId, messageThreadId, taskId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTask>> GetTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await taskService.GetAsync(taskId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<TelegramTaskReviewSnapshot>> GetTaskReviewAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await taskService.GetAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return ApplicationResult<TelegramTaskReviewSnapshot>.Failure(
                task.Error!.Code,
                task.Error.Message);
        }

        var iterations = await iterationService.ListByTaskAsync(taskId, cancellationToken);
        var artifacts = await artifactService.ListByTaskAsync(taskId, cancellationToken);
        var executions = await runnerExecutionService.ListByTaskAsync(taskId, cancellationToken);
        var runtimeEvents = await runtimeEventService.ListByTaskAsync(taskId, 10, cancellationToken);
        var latestResponse = TryReadLatestRunnerResponse(artifacts, iterations);

        return ApplicationResult<TelegramTaskReviewSnapshot>.Success(
            new TelegramTaskReviewSnapshot(
                task.Value!,
                iterations,
                artifacts,
                runtimeLayout.ArtifactsPath,
                executions,
                runtimeEvents,
                latestResponse));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeEvent>> ListTaskRuntimeEventsAsync(
        TaskId taskId,
        int limit,
        CancellationToken cancellationToken) =>
        await runtimeEventService.ListByTaskAsync(taskId, limit, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTask>> CancelTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await taskService.CancelAsync(taskId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTask>> CompleteTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await taskService.CompleteAsync(taskId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<RuntimeTask>> ContinueTaskAsync(
        TaskId taskId,
        string feedback,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(feedback))
        {
            return ApplicationResult<RuntimeTask>.Failure(
                "empty_review_feedback",
                "Review feedback must not be empty.");
        }

        var task = await taskService.GetAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return ApplicationResult<RuntimeTask>.Failure(task.Error!.Code, task.Error.Message);
        }

        if (task.Value!.Status != RuntimeTaskStatus.Reviewing)
        {
            return ApplicationResult<RuntimeTask>.Failure(
                "task_not_reviewing",
                $"Task '{taskId}' is not waiting for review feedback.");
        }

        if (task.Value.CurrentIteration >= task.Value.MaxIterations)
        {
            return ApplicationResult<RuntimeTask>.Failure(
                "iteration_limit_reached",
                $"Task '{taskId}' cannot continue because it reached {task.Value.MaxIterations} iterations.");
        }

        var iterations = await iterationService.ListByTaskAsync(taskId, cancellationToken);
        var latestIterationId = iterations
            .OrderByDescending(iteration => iteration.IterationNumber)
            .FirstOrDefault()
            ?.Id;
        var artifactId = ArtifactId.New();
        var artifactContent = CreateReviewFeedbackContent(task.Value, feedback);
        var artifactBytes = Encoding.UTF8.GetBytes(artifactContent);
        var relativePath = CreateReviewArtifactPath(task.Value, artifactId);
        var fullPath = ResolveArtifactPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, artifactContent, Encoding.UTF8, cancellationToken);

        var artifact = await artifactService.RegisterAsync(
            new RegisterArtifactRequest(
                taskId,
                latestIterationId,
                ArtifactType.Review,
                relativePath,
                artifactBytes.LongLength,
                Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant(),
                artifactId),
            cancellationToken);

        if (!artifact.IsSuccess)
        {
            return ApplicationResult<RuntimeTask>.Failure(artifact.Error!.Code, artifact.Error.Message);
        }

        return await taskService.RequeueForRevisionAsync(taskId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<ApprovalRequest>> GetApprovalAsync(
        ApprovalId approvalId,
        CancellationToken cancellationToken) =>
        await approvalService.GetAsync(approvalId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<ApprovalRequest>> ApproveApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken) =>
        await approvalService.ApproveAsync(new ResolveApprovalRequest(approvalId, resolvedBy), cancellationToken);

    /// <inheritdoc />
    public async Task<ApplicationResult<ApprovalRequest>> RejectApprovalAsync(
        ApprovalId approvalId,
        string resolvedBy,
        CancellationToken cancellationToken) =>
        await approvalService.RejectAsync(new ResolveApprovalRequest(approvalId, resolvedBy), cancellationToken);

    private TelegramRunnerSettings CreateRunnerSettings() =>
        new(
            configuration.Runners.Codex.SandboxMode,
            configuration.Runners.Codex.BypassApprovalsAndSandbox,
            configuration.Agent.MaxParallelTasks);

    private async Task SaveConfigurationAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(configPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(
            stream,
            configuration,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            },
            cancellationToken);

        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }

    private static string ResolveExecutablePath() =>
        ResolveFromPath("aeges") ?? Environment.ProcessPath ?? "aeges";

    private static string? ResolveFromPath(string executable)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executable);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value));

    private static string CreateReviewFeedbackContent(RuntimeTask task, string feedback) =>
        $"""
        # Review Feedback

        Task ID: {task.Id}
        Previous iteration: {task.CurrentIteration}

        Feedback:
        {feedback.Trim()}
        """;

    private static string CreateReviewArtifactPath(RuntimeTask task, ArtifactId artifactId) =>
        string.Join(
            '/',
            SanitizePathSegment(task.ProjectId.Value),
            SanitizePathSegment(task.Id.Value),
            "review",
            $"{SanitizePathSegment(artifactId.Value)}.md");

    private string ResolveArtifactPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(runtimeLayout.ArtifactsPath, relativePath));
        var artifactRoot = Path.GetFullPath(runtimeLayout.ArtifactsPath);

        if (Path.GetRelativePath(artifactRoot, fullPath).StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Review feedback artifact path escaped the configured artifact root.");
        }

        return fullPath;
    }

    private static string SanitizePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.ToString();
    }

    private string? TryReadLatestRunnerResponse(
        IReadOnlyList<RuntimeArtifact> artifacts,
        IReadOnlyList<TaskIteration> iterations)
    {
        var preferredArtifacts = artifacts
            .Where(artifact => artifact.Type is ArtifactType.Result or ArtifactType.StdoutLog)
            .OrderByDescending(artifact => artifact.CreatedAt)
            .ToArray();

        foreach (var artifact in preferredArtifacts)
        {
            var preview = TryReadArtifactPreview(artifact);

            if (!string.IsNullOrWhiteSpace(preview))
            {
                return preview;
            }
        }

        foreach (var iteration in iterations.OrderByDescending(iteration => iteration.IterationNumber))
        {
            var promptArtifact = artifacts
                .Where(artifact => artifact.IterationId == iteration.Id && artifact.Type is ArtifactType.Prompt)
                .OrderByDescending(artifact => artifact.CreatedAt)
                .FirstOrDefault();
            var discoveredPreview = promptArtifact is null ? null : TryReadDiscoveredCodexPreview(promptArtifact);

            if (!string.IsNullOrWhiteSpace(discoveredPreview))
            {
                return discoveredPreview;
            }
        }

        return null;
    }

    private string? TryReadArtifactPreview(RuntimeArtifact artifact)
    {
        var fullPath = Path.Combine(runtimeLayout.ArtifactsPath, artifact.RelativePath);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        return artifact.Type is ArtifactType.StdoutLog && fullPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
            ? TryReadCodexJsonlPreview(fullPath)
            : TryReadTextPreview(fullPath);
    }

    private string? TryReadDiscoveredCodexPreview(RuntimeArtifact promptArtifact)
    {
        var promptPath = Path.Combine(runtimeLayout.ArtifactsPath, promptArtifact.RelativePath);
        var codexStdoutPath = Path.Combine(Path.GetDirectoryName(promptPath)!, "codex.stdout.jsonl");

        return File.Exists(codexStdoutPath)
            ? TryReadCodexJsonlPreview(codexStdoutPath)
            : null;
    }

    private static string? TryReadCodexJsonlPreview(string path)
    {
        string? latestMessage = null;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (!root.TryGetProperty("type", out var type)
                    || type.GetString() != "item.completed"
                    || !root.TryGetProperty("item", out var item)
                    || !item.TryGetProperty("type", out var itemType)
                    || itemType.GetString() != "agent_message"
                    || !item.TryGetProperty("text", out var text))
                {
                    continue;
                }

                latestMessage = text.GetString();
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return TrimPreview(latestMessage);
    }

    private static string? TryReadTextPreview(string path)
    {
        using var reader = File.OpenText(path);
        var buffer = new char[1600];
        var count = reader.Read(buffer, 0, buffer.Length);

        return TrimPreview(new string(buffer, 0, count));
    }

    private static string? TrimPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        return normalized.Length <= 1200
            ? normalized
            : normalized[..1200].TrimEnd() + "\n...";
    }
}
