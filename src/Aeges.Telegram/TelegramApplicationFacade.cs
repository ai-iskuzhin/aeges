using Aeges.Application;
using Aeges.Application.Approvals;
using Aeges.Application.Artifacts;
using Aeges.Application.Configuration;
using Aeges.Application.Iterations;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.RunnerExecutions;
using Aeges.Application.Runtime;
using Aeges.Application.Tasks;
using Aeges.Core;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Aeges.Telegram;

/// <summary>
/// Application-service backed implementation of <see cref="ITelegramApplicationFacade"/>.
/// </summary>
public sealed class TelegramApplicationFacade : ITelegramApplicationFacade
{
    private readonly ProjectService projectService;
    private readonly MachineService machineService;
    private readonly TaskService taskService;
    private readonly TaskIterationService iterationService;
    private readonly ArtifactService artifactService;
    private readonly RunnerExecutionService runnerExecutionService;
    private readonly ApprovalService approvalService;
    private readonly RuntimeDirectoryLayout runtimeLayout;
    private readonly AegesConfiguration configuration;
    private readonly string configPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramApplicationFacade"/> class.
    /// </summary>
    /// <param name="projectService">The project application service.</param>
    /// <param name="machineService">The machine application service.</param>
    /// <param name="taskService">The task application service.</param>
    /// <param name="iterationService">The task iteration application service.</param>
    /// <param name="artifactService">The artifact application service.</param>
    /// <param name="runnerExecutionService">The runner execution application service.</param>
    /// <param name="approvalService">The approval application service.</param>
    /// <param name="configuration">The loaded local runtime configuration.</param>
    /// <param name="configPath">The configuration file path to update for settings changes.</param>
    /// <param name="runtimeLayout">The runtime directory layout used to resolve local artifact previews.</param>
    public TelegramApplicationFacade(
        ProjectService projectService,
        MachineService machineService,
        TaskService taskService,
        TaskIterationService iterationService,
        ArtifactService artifactService,
        RunnerExecutionService runnerExecutionService,
        ApprovalService approvalService,
        AegesConfiguration configuration,
        string configPath,
        RuntimeDirectoryLayout? runtimeLayout = null)
    {
        this.projectService = projectService;
        this.machineService = machineService;
        this.taskService = taskService;
        this.iterationService = iterationService;
        this.artifactService = artifactService;
        this.runnerExecutionService = runnerExecutionService;
        this.approvalService = approvalService;
        this.configuration = configuration;
        this.configPath = configPath;
        this.runtimeLayout = runtimeLayout ?? RuntimeDirectoryLayout.CreateDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken) =>
        await projectService.ListAsync(cancellationToken);

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
        var latestResponse = TryReadLatestRunnerResponse(artifacts, iterations);

        return ApplicationResult<TelegramTaskReviewSnapshot>.Success(
            new TelegramTaskReviewSnapshot(
                task.Value!,
                iterations,
                artifacts,
                executions,
                latestResponse));
    }

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
            configuration.Runners.Codex.BypassApprovalsAndSandbox);

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
