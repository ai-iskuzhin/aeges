using Aeges.Application.Configuration;
using Aeges.Core;
using System.Collections.Concurrent;

namespace Aeges.Telegram;

/// <summary>
/// Handles Telegram interactions using button callback payloads.
/// </summary>
public sealed class TelegramInteractionHandler
{
    private const int DefaultTaskLimit = 10;
    private const int DefaultApprovalLimit = 10;
    private const int MenuCountLimit = 100;
    private const int ButtonGridColumns = 2;
    private readonly ITelegramApplicationFacade application;
    private readonly ITelegramCallbackRegistry? callbackRegistry;
    private readonly HashSet<long> allowedChatIds;
    private readonly ConcurrentDictionary<long, TaskDraft> drafts = new();
    private readonly ConcurrentDictionary<long, TaskId> continuationDrafts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramInteractionHandler"/> class.
    /// </summary>
    /// <param name="application">The Telegram application facade.</param>
    /// <param name="configuration">The Telegram configuration.</param>
    /// <param name="callbackRegistry">The optional callback registry used to shorten Telegram callback payloads.</param>
    public TelegramInteractionHandler(
        ITelegramApplicationFacade application,
        AegesTelegramConfiguration configuration,
        ITelegramCallbackRegistry? callbackRegistry = null)
    {
        this.application = application;
        this.callbackRegistry = callbackRegistry;
        allowedChatIds = [.. configuration.AllowedChatIds];
    }

    /// <summary>
    /// Handles an inbound Telegram update.
    /// </summary>
    /// <param name="update">The inbound update.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The response to send back to Telegram.</returns>
    public async Task<TelegramResponse> HandleAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(update.ChatId))
        {
            return new TelegramResponse(
                "This Telegram chat is not authorized for Aeges.",
                TelegramButtonMarkup.Empty);
        }

        var callbackData = await ResolveCallbackDataAsync(update.ChatId, update.CallbackData?.Trim(), cancellationToken);

        if (callbackData is null or "")
        {
            return await TokenizeResponseAsync(update.ChatId, await HandleTextAsync(update, cancellationToken), cancellationToken);
        }

        var response = callbackData switch
        {
            TelegramCallbackData.MainMenu => await MainMenuAsync(cancellationToken),
            TelegramCallbackData.ListProjects => await ListProjectsAsync(cancellationToken),
            TelegramCallbackData.ListMachines => await ListMachinesAsync(cancellationToken),
            TelegramCallbackData.ListQueuedTasks => await ListQueuedTasksAsync(cancellationToken),
            TelegramCallbackData.TaskMenu => await TaskMenuAsync(cancellationToken),
            TelegramCallbackData.ListPendingApprovals => await ListPendingApprovalsAsync(cancellationToken),
            TelegramCallbackData.SettingsMenu => await SettingsMenuAsync(cancellationToken),
            TelegramCallbackData.CancelPendingTextResponse => PendingTextResponseAlreadyFinished(),
            TelegramCallbackData.CreateTask => await StartTaskCreationAsync(update.ChatId, cancellationToken),
            TelegramCallbackData.CancelCreateTask => CancelTaskCreation(update.ChatId),
            TelegramCallbackData.CancelContinueTask => CancelTaskContinuation(update.ChatId),
            _ when TelegramCallbackData.TryParseSetCodexSandboxMode(callbackData, out var sandboxMode) =>
                await SetCodexSandboxModeAsync(sandboxMode, cancellationToken),
            _ when TelegramCallbackData.TryParseSetCodexBypassApprovalsAndSandbox(callbackData, out var bypassEnabled) =>
                await SetCodexBypassApprovalsAndSandboxAsync(bypassEnabled, cancellationToken),
            _ when TelegramCallbackData.TryParseListTasksByStatus(callbackData, out var status) =>
                await ListTasksByStatusAsync(status, cancellationToken),
            _ when TelegramCallbackData.TryParseListProjectTasksByStatus(callbackData, out var projectId, out var status) =>
                await ListProjectTasksByStatusAsync(projectId, status, cancellationToken),
            _ when TelegramCallbackData.TryParseArchiveProject(callbackData, out var projectId) =>
                await ArchiveProjectAsync(projectId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewProject(callbackData, out var projectId) =>
                await ViewProjectAsync(projectId, cancellationToken),
            _ when TelegramCallbackData.TryParseSelectTaskProject(callbackData, out var projectId) =>
                await SelectTaskProjectAsync(update.ChatId, projectId, cancellationToken),
            _ when TelegramCallbackData.TryParseSelectTaskMachine(callbackData, out var machineId) =>
                await SelectTaskMachineAsync(update.ChatId, machineId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewTask(callbackData, out var taskId) =>
                await ViewTaskAsync(taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseCompleteTask(callbackData, out var taskId) =>
                await CompleteTaskAsync(taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseContinueTask(callbackData, out var taskId) =>
                await StartTaskContinuationAsync(update.ChatId, taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseCancelTask(callbackData, out var taskId) =>
                await CancelTaskAsync(taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseApproveApproval(callbackData, out var approvalId) =>
                await ResolveApprovalAsync(approvalId, approved: true, update.ChatId, cancellationToken),
            _ when TelegramCallbackData.TryParseRejectApproval(callbackData, out var approvalId) =>
                await ResolveApprovalAsync(approvalId, approved: false, update.ChatId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewApproval(callbackData, out var approvalId) =>
                await ViewApprovalAsync(approvalId, cancellationToken),
            _ => UnknownAction(),
        };

        return await TokenizeResponseAsync(update.ChatId, response, cancellationToken);
    }

    /// <summary>
    /// Rewrites response button callbacks into short transport-safe tokens when a registry is configured.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="response">The response to tokenize.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The response with tokenized callbacks.</returns>
    public async Task<TelegramResponse> TokenizeResponseAsync(
        long chatId,
        TelegramResponse response,
        CancellationToken cancellationToken) =>
        callbackRegistry is null
            ? response
            : await callbackRegistry.TokenizeAsync(chatId, response, cancellationToken);

    private async Task<string?> ResolveCallbackDataAsync(
        long chatId,
        string? callbackData,
        CancellationToken cancellationToken)
    {
        if (callbackData is null or "")
        {
            return callbackData;
        }

        return callbackRegistry is null
            ? callbackData
            : await callbackRegistry.ResolveAsync(chatId, callbackData, cancellationToken);
    }

    /// <summary>
    /// Renders the temporary response shown while an inbound text message is being processed.
    /// </summary>
    /// <param name="message">The inbound operator message.</param>
    /// <returns>The pending response with a cancellation button.</returns>
    public static TelegramResponse RenderPendingTextResponse(string? message) =>
        new(
            $"""
            Working on your message...

            Message:
            {TelegramMarkdown.Quote(string.IsNullOrWhiteSpace(message) ? "(empty)" : message.Trim())}
            """,
            Buttons(Row(Button("Cancel", TelegramCallbackData.CancelPendingTextResponse))));

    private bool IsAuthorized(long chatId) =>
        allowedChatIds.Count == 0 || allowedChatIds.Contains(chatId);

    private async Task<TelegramResponse> MainMenuAsync(CancellationToken cancellationToken)
    {
        var projects = await application.ListProjectsAsync(cancellationToken);
        var machines = await application.ListMachinesAsync(cancellationToken);
        var queuedTasks = await application.ListQueuedTasksAsync(MenuCountLimit, cancellationToken);
        var approvals = await application.ListPendingApprovalsAsync(MenuCountLimit, cancellationToken);

        return new TelegramResponse(
            "Aeges control",
            Buttons(
                Row(
                    Button("New task", TelegramCallbackData.CreateTask),
                    Button("Settings", TelegramCallbackData.SettingsMenu)),
                Row(
                    Button($"Projects ({projects.Count})", TelegramCallbackData.ListProjects),
                    Button($"Machines ({machines.Count})", TelegramCallbackData.ListMachines)),
                Row(
                    Button($"Tasks ({CountBadge(queuedTasks.Count, MenuCountLimit)} queued)", TelegramCallbackData.TaskMenu),
                    Button($"Approvals ({CountBadge(approvals.Count, MenuCountLimit)})", TelegramCallbackData.ListPendingApprovals))));
    }

    private async Task<TelegramResponse> HandleTextAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        var text = update.Text?.Trim();

        if (continuationDrafts.TryGetValue(update.ChatId, out var continuationTaskId))
        {
            return string.IsNullOrWhiteSpace(text)
                ? new TelegramResponse("Send non-empty feedback, or cancel task continuation.", ContinueDraftButtons())
                : await ContinueTaskWithFeedbackAsync(update.ChatId, continuationTaskId, text, cancellationToken);
        }

        if (!drafts.TryGetValue(update.ChatId, out var draft))
        {
            return await SendTalkMessageAsync(update.ChatId, text, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TelegramResponse("Send non-empty text, or cancel task creation.", CancelDraftButtons());
        }

        if (draft.Step == TaskDraftStep.AwaitingTitle)
        {
            drafts[update.ChatId] = draft with
            {
                Title = text,
                Step = TaskDraftStep.AwaitingGoal,
            };

            return new TelegramResponse("Now send the task goal/details.", CancelDraftButtons());
        }

        if (draft.Step != TaskDraftStep.AwaitingGoal || draft.Title is null || draft.MachineId is null)
        {
            drafts.TryRemove(update.ChatId, out _);
            return await MainMenuAsync(cancellationToken);
        }

        var result = await application.CreateTaskAsync(
            draft.ProjectId,
            draft.MachineId.Value,
            draft.Title,
            text,
            cancellationToken);
        drafts.TryRemove(update.ChatId, out _);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        var agentNotice = await StartAgentNoticeAsync(cancellationToken);

        return new TelegramResponse(
            $"""
            Task queued: {result.Value!.Id}
            Project: {result.Value.ProjectId}
            Machine: {result.Value.MachineId}
            Title: {result.Value.Title}

            Goal:
            {TelegramMarkdown.Quote(text)}

            Agent:
            {TelegramMarkdown.Quote(agentNotice)}
            """,
            Buttons(
                Row(
                    Button("View task", TelegramCallbackData.ViewTask(result.Value.Id)),
                    Button("Back", TelegramCallbackData.MainMenu))),
            new TelegramResponseMetadata(TelegramResponseKind.TaskDetails, result.Value.Id));
    }

    private async Task<TelegramResponse> StartTaskCreationAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        var projects = await application.ListActiveProjectsAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return new TelegramResponse("No active projects are registered. Add a project from the CLI first.", BackButtons());
        }

        drafts.TryRemove(chatId, out _);
        continuationDrafts.TryRemove(chatId, out _);
        var rows = Grid(projects.Select(project => Button(project.Name, TelegramCallbackData.SelectTaskProject(project.Id))))
            .Append(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)))
            .ToArray();

        return new TelegramResponse("Choose a project for the task.", Buttons(rows));
    }

    private async Task<TelegramResponse> SendTalkMessageAsync(
        long chatId,
        string? text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return await MainMenuAsync(cancellationToken);
        }

        var result = await application.SendTalkMessageAsync(chatId, text, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse(
                $"{result.Error!.Code}: {result.Error.Message}",
                Buttons(Row(
                    Button("Menu", TelegramCallbackData.MainMenu),
                    Button("New task", TelegramCallbackData.CreateTask))));
        }

        var exchange = result.Value!;

        return new TelegramResponse(
            $"""
            {TelegramMarkdown.Quote($"Session: {exchange.Session.Id}\nRunner: {exchange.Session.RunnerId}")}

            {exchange.AssistantMessage.Content}
            """,
            Buttons(Row(
                Button("Menu", TelegramCallbackData.MainMenu),
                Button("New task", TelegramCallbackData.CreateTask))));
    }

    private async Task<TelegramResponse> SelectTaskProjectAsync(
        long chatId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = await application.GetProjectAsync(projectId, cancellationToken);

        if (!project.IsSuccess)
        {
            return new TelegramResponse($"Project '{projectId}' was not found.", BackButtons());
        }

        if (project.Value!.IsArchived)
        {
            return new TelegramResponse($"Project '{projectId}' is archived and cannot accept new tasks.", BackButtons());
        }

        var machines = await application.ListMachinesAsync(cancellationToken);

        if (machines.Count == 0)
        {
            return new TelegramResponse("No machines are registered. Add a machine from the CLI first.", BackButtons());
        }

        drafts[chatId] = new TaskDraft(projectId, MachineId: null, null, TaskDraftStep.ChoosingMachine);
        var rows = Grid(machines.Select(machine => Button(
                $"{machine.Name} ({machine.Status.ToStorageValue()})",
                TelegramCallbackData.SelectTaskMachine(machine.Id))))
            .Append(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)))
            .ToArray();

        return new TelegramResponse("Choose the machine that should process the task.", Buttons(rows));
    }

    private Task<TelegramResponse> SelectTaskMachineAsync(
        long chatId,
        MachineId machineId,
        CancellationToken cancellationToken)
    {
        if (!drafts.TryGetValue(chatId, out var draft) || draft.Step != TaskDraftStep.ChoosingMachine)
        {
            return Task.FromResult(new TelegramResponse("Start task creation first.", BackButtons()));
        }

        drafts[chatId] = draft with
        {
            MachineId = machineId,
            Step = TaskDraftStep.AwaitingTitle,
        };

        return Task.FromResult(new TelegramResponse("Send the task title.", CancelDraftButtons()));
    }

    private TelegramResponse CancelTaskCreation(long chatId)
    {
        drafts.TryRemove(chatId, out _);

        return new TelegramResponse("Task creation cancelled.", BackButtons());
    }

    private async Task<TelegramResponse> StartTaskContinuationAsync(
        long chatId,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        if (task.Value!.Status != RuntimeTaskStatus.Reviewing)
        {
            return new TelegramResponse("Only reviewing tasks can be continued.", BackButtons());
        }

        if (task.Value.CurrentIteration >= task.Value.MaxIterations)
        {
            return new TelegramResponse("This task has reached its iteration limit.", BackButtons());
        }

        drafts.TryRemove(chatId, out _);
        continuationDrafts[chatId] = taskId;

        return new TelegramResponse(
            "Send the follow-up instructions for the next iteration.",
            ContinueDraftButtons());
    }

    private async Task<TelegramResponse> ContinueTaskWithFeedbackAsync(
        long chatId,
        TaskId taskId,
        string feedback,
        CancellationToken cancellationToken)
    {
        var result = await application.ContinueTaskAsync(taskId, feedback, cancellationToken);
        continuationDrafts.TryRemove(chatId, out _);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        var agentNotice = await StartAgentNoticeAsync(cancellationToken);

        return new TelegramResponse(
            $"""
            Task continued: {result.Value!.Id}
            Status: {result.Value.Status.ToStorageValue()}

            Follow-up:
            {TelegramMarkdown.Quote(feedback)}

            Agent:
            {TelegramMarkdown.Quote(agentNotice)}
            """,
            Buttons(
                Row(
                    Button("View task", TelegramCallbackData.ViewTask(result.Value.Id)),
                    Button("Back", TelegramCallbackData.MainMenu))),
            new TelegramResponseMetadata(TelegramResponseKind.TaskDetails, result.Value.Id));
    }

    private TelegramResponse CancelTaskContinuation(long chatId)
    {
        continuationDrafts.TryRemove(chatId, out _);

        return new TelegramResponse("Task continuation cancelled.", BackButtons());
    }

    private async Task<TelegramResponse> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = await application.ListProjectsAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return new TelegramResponse("No projects are registered.", BackButtons());
        }

        var rows = Grid(projects.Select(project => Button(ProjectButtonText(project), TelegramCallbackData.ViewProject(project.Id))))
            .Append(Row(Button("Back", TelegramCallbackData.MainMenu)))
            .ToArray();

        return new TelegramResponse("Projects", Buttons(rows));
    }

    private async Task<TelegramResponse> ViewProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = await application.GetProjectAsync(projectId, cancellationToken);

        if (!project.IsSuccess)
        {
            return new TelegramResponse($"{project.Error!.Code}: {project.Error.Message}", BackToProjectsButtons());
        }

        return await RenderProjectDetailsAsync(project.Value!, notice: null, cancellationToken);
    }

    private async Task<TelegramResponse> ArchiveProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var archived = await application.ArchiveProjectAsync(projectId, cancellationToken);

        if (!archived.IsSuccess)
        {
            return new TelegramResponse($"{archived.Error!.Code}: {archived.Error.Message}", BackToProjectsButtons());
        }

        return await RenderProjectDetailsAsync(archived.Value!, "Project archived.", cancellationToken);
    }

    private async Task<TelegramResponse> RenderProjectDetailsAsync(
        RuntimeProject project,
        string? notice,
        CancellationToken cancellationToken)
    {
        var buttons = new List<TelegramButton>();

        foreach (var status in TaskStatuses)
        {
            var tasks = await application.ListProjectTasksByStatusAsync(
                project.Id,
                status,
                MenuCountLimit,
                cancellationToken);

            buttons.Add(Button(
                $"{FormatStatus(status)} ({CountBadge(tasks.Count, MenuCountLimit)})",
                TelegramCallbackData.ListProjectTasksByStatus(project.Id, status)));
        }

        IReadOnlyList<TelegramButton> actionButtons = project.IsArchived
            ? []
            : [Button("Archive", TelegramCallbackData.ArchiveProject(project.Id))];
        var rows = Grid(buttons.Concat(actionButtons))
            .Append(Row(Button("Back", TelegramCallbackData.ListProjects)))
            .ToArray();

        List<string> metadataLines =
        [
            $"Project: {project.Id}",
            $"Name: {project.Name}",
            $"Status: {(project.IsArchived ? "archived" : "active")}",
            $"Path: {project.Path}",
        ];

        if (project.ArchivedAt is not null)
        {
            metadataLines.Add($"Archived: {project.ArchivedAt:O}");
        }

        var metadata = string.Join('\n', metadataLines);

        var prefix = string.IsNullOrWhiteSpace(notice)
            ? "Project details:"
            : $"{notice}\n\nProject details:";

        return new TelegramResponse(
            $"{prefix}\n{TelegramMarkdown.Quote(metadata)}",
            Buttons([.. rows]));
    }

    private async Task<TelegramResponse> ListProjectTasksByStatusAsync(
        ProjectId projectId,
        RuntimeTaskStatus status,
        CancellationToken cancellationToken)
    {
        var project = await application.GetProjectAsync(projectId, cancellationToken);

        if (!project.IsSuccess)
        {
            return new TelegramResponse($"{project.Error!.Code}: {project.Error.Message}", BackToProjectsButtons());
        }

        var tasks = await application.ListProjectTasksByStatusAsync(
            projectId,
            status,
            DefaultTaskLimit,
            cancellationToken);
        var statusText = FormatStatus(status);

        if (tasks.Count == 0)
        {
            return new TelegramResponse(
                $"No {statusText} tasks for {project.Value!.Name}.",
                ProjectButtons(projectId));
        }

        var buttons = Grid(tasks.Select(task => Button(task.Title, TelegramCallbackData.ViewTask(task.Id))))
            .Append(Row(Button("Back", TelegramCallbackData.ViewProject(projectId))))
            .ToArray();

        return new TelegramResponse(
            $"{project.Value!.Name} {statusText} tasks:\n"
            + string.Join('\n', tasks.Select(task => $"- {task.Id}: {task.Title}")),
            Buttons(buttons));
    }

    private async Task<TelegramResponse> ListMachinesAsync(CancellationToken cancellationToken)
    {
        var machines = await application.ListMachinesAsync(cancellationToken);
        var text = machines.Count == 0
            ? "No machines are registered."
            : "Machines:\n" + string.Join(
                '\n',
                machines.Select(machine =>
                    $"- {machine.Id}: {machine.Name} ({machine.Status.ToStorageValue()}, last seen {FormatLastSeen(machine.LastSeenAt)})"));

        return new TelegramResponse(text, BackButtons());
    }

    private async Task<TelegramResponse> ListQueuedTasksAsync(CancellationToken cancellationToken)
    {
        return await ListTasksByStatusAsync(RuntimeTaskStatus.Queued, cancellationToken);
    }

    private async Task<TelegramResponse> TaskMenuAsync(CancellationToken cancellationToken)
    {
        var buttons = new List<TelegramButton>();

        foreach (var status in TaskStatuses)
        {
            var tasks = await application.ListTasksByStatusAsync(status, MenuCountLimit, cancellationToken);
            buttons.Add(Button(
                $"{FormatStatus(status)} ({CountBadge(tasks.Count, MenuCountLimit)})",
                TelegramCallbackData.ListTasksByStatus(status)));
        }

        var rows = Grid(buttons)
            .Append(Row(Button("Back", TelegramCallbackData.MainMenu)))
            .ToArray();

        return new TelegramResponse("Tasks by status", Buttons(rows));
    }

    private async Task<TelegramResponse> ListTasksByStatusAsync(
        RuntimeTaskStatus status,
        CancellationToken cancellationToken)
    {
        var tasks = await application.ListTasksByStatusAsync(status, DefaultTaskLimit, cancellationToken);
        var statusText = FormatStatus(status);

        if (tasks.Count == 0)
        {
            return new TelegramResponse($"No {statusText} tasks.", TaskMenuButtons());
        }

        var buttons = Grid(tasks.Select(task => Button(task.Title, TelegramCallbackData.ViewTask(task.Id))))
            .Append(Row(Button("Back", TelegramCallbackData.TaskMenu)))
            .ToArray();

        return new TelegramResponse(
            $"{statusText} tasks:\n" + string.Join('\n', tasks.Select(task => $"- {task.Id}: {task.Title}")),
            Buttons(buttons));
    }

    private async Task<TelegramResponse> ListPendingApprovalsAsync(CancellationToken cancellationToken)
    {
        var approvals = await application.ListPendingApprovalsAsync(DefaultApprovalLimit, cancellationToken);

        if (approvals.Count == 0)
        {
            return new TelegramResponse("No pending approvals.", BackButtons());
        }

        var buttons = Grid(approvals.Select(approval => Button(approval.Id.Value, TelegramCallbackData.ViewApproval(approval.Id))))
            .Append(Row(Button("Back", TelegramCallbackData.MainMenu)))
            .ToArray();

        return new TelegramResponse(
            "Pending approvals:\n" + string.Join(
                '\n',
                approvals.Select(approval => $"- {approval.Id}: {approval.RequestedAction}")),
            Buttons(buttons));
    }

    private async Task<TelegramResponse> SettingsMenuAsync(CancellationToken cancellationToken)
    {
        var settings = await application.GetRunnerSettingsAsync(cancellationToken);

        return RenderSettings(settings);
    }

    private async Task<TelegramResponse> SetCodexSandboxModeAsync(
        string sandboxMode,
        CancellationToken cancellationToken)
    {
        var result = await application.SetCodexSandboxModeAsync(sandboxMode, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        return RenderSettings(result.Value!, await RestartAgentNoticeAsync(cancellationToken));
    }

    private async Task<TelegramResponse> SetCodexBypassApprovalsAndSandboxAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        var result = await application.SetCodexBypassApprovalsAndSandboxAsync(enabled, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        return RenderSettings(result.Value!, await RestartAgentNoticeAsync(cancellationToken));
    }

    private async Task<string> RestartAgentNoticeAsync(CancellationToken cancellationToken)
    {
        var restart = await application.RestartAgentAsync(cancellationToken);

        return restart.IsSuccess
            ? $"Agent restart: {restart.Value!.Message}"
            : $"Agent restart failed: {restart.Error!.Code}: {restart.Error.Message}";
    }

    private async Task<string> StartAgentNoticeAsync(CancellationToken cancellationToken)
    {
        var start = await application.StartAgentAsync(cancellationToken);

        return start.IsSuccess
            ? start.Value!.Message
            : $"Could not start agent automatically: {start.Error!.Code}: {start.Error.Message}";
    }

    public async Task<TelegramResponse> RenderTaskDetailsAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var review = await application.GetTaskReviewAsync(taskId, cancellationToken);

        if (!review.IsSuccess)
        {
            return new TelegramResponse($"{review.Error!.Code}: {review.Error.Message}", BackButtons());
        }

        var snapshot = review.Value!;
        var task = snapshot.Task;
        var latestIteration = snapshot.Iterations
            .OrderByDescending(iteration => iteration.IterationNumber)
            .FirstOrDefault();
        var latestExecution = latestIteration is null
            ? null
            : snapshot.RunnerExecutions
                .Where(execution => execution.IterationId == latestIteration.Id)
                .OrderByDescending(execution => execution.StartedAt)
                .FirstOrDefault();
        var artifactLines = snapshot.Artifacts.Count == 0
            ? "(none)"
            : string.Join(
                '\n',
                snapshot.Artifacts
                    .OrderBy(artifact => artifact.CreatedAt)
                    .Select(artifact =>
                        $"- {artifact.Type.ToStorageValue()}: {FormatArtifactPath(snapshot.ArtifactRootPath, artifact.RelativePath)}"));
        var runnerResponse = snapshot.LatestRunnerResponse is null
            ? "(none yet)"
            : snapshot.LatestRunnerResponse;
        var taskMetadata = string.Join(
            '\n',
            [
                $"Task: {task.Id}",
                $"Title: {task.Title}",
                $"Status: {task.Status.ToStorageValue()}",
                $"Iterations: {task.CurrentIteration}/{task.MaxIterations}",
                $"Latest iteration: {FormatIteration(latestIteration)}",
                $"Runner: {FormatRunnerExecution(latestExecution)}",
            ]);

        return new TelegramResponse(
            $"""
            Task details:
            {TelegramMarkdown.Quote(taskMetadata)}

            Goal:
            {TelegramMarkdown.Quote(task.Goal)}

            Runner response:
            {TelegramMarkdown.Quote(runnerResponse)}

            Artifacts:
            {TelegramMarkdown.Quote(artifactLines)}
            """,
            TaskDetailButtons(task),
            new TelegramResponseMetadata(TelegramResponseKind.TaskDetails, task.Id));
    }

    private async Task<TelegramResponse> ViewTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await RenderTaskDetailsAsync(taskId, cancellationToken);

    public async Task<RuntimeTask?> GetTaskOrDefaultAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        return task.IsSuccess ? task.Value : null;
    }

    private async Task<TelegramResponse> CancelTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.CancelTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        return new TelegramResponse(
            $"Task cancelled: {task.Value!.Id}",
            BackButtons());
    }

    private async Task<TelegramResponse> CompleteTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.CompleteTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        var response = await ListTasksByStatusAsync(RuntimeTaskStatus.Completed, cancellationToken);

        return response with
        {
            Metadata = new TelegramResponseMetadata(TelegramResponseKind.TaskWatch, task.Value!.Id),
        };
    }

    private async Task<TelegramResponse> ViewApprovalAsync(
        ApprovalId approvalId,
        CancellationToken cancellationToken)
    {
        var approval = await application.GetApprovalAsync(approvalId, cancellationToken);

        if (!approval.IsSuccess)
        {
            return new TelegramResponse($"{approval.Error!.Code}: {approval.Error.Message}", BackButtons());
        }

        var value = approval.Value!;

        return new TelegramResponse(
            $"""
            Approval: {value.Id}
            Task: {value.TaskId}
            Status: {value.Status.ToStorageValue()}
            Action: {value.RequestedAction}

            Reason:
            {TelegramMarkdown.Quote(value.Reason)}
            """,
            Buttons(
                Row(
                    Button("Approve", TelegramCallbackData.ApproveApproval(value.Id)),
                    Button("Reject", TelegramCallbackData.RejectApproval(value.Id))),
                Row(Button("Back", TelegramCallbackData.ListPendingApprovals))));
    }

    private async Task<TelegramResponse> ResolveApprovalAsync(
        ApprovalId approvalId,
        bool approved,
        long chatId,
        CancellationToken cancellationToken)
    {
        var resolvedBy = $"telegram:{chatId}";
        var result = approved
            ? await application.ApproveApprovalAsync(approvalId, resolvedBy, cancellationToken)
            : await application.RejectApprovalAsync(approvalId, resolvedBy, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        var status = result.Value!.Status.ToStorageValue();

        return new TelegramResponse(
            $"Approval {status}: {result.Value.Id}",
            BackButtons());
    }

    private static TelegramResponse UnknownAction() =>
        new("Unknown action. Send any message to open the Aeges menu.", TelegramButtonMarkup.Empty);

    private static TelegramResponse PendingTextResponseAlreadyFinished() =>
        new(
            "This message is already finished, or cancellation is not available for this processing step.",
            Buttons(Row(
                Button("Menu", TelegramCallbackData.MainMenu),
                Button("New task", TelegramCallbackData.CreateTask))));

    private static TelegramButtonMarkup BackButtons() =>
        Buttons(Row(Button("Back", TelegramCallbackData.MainMenu)));

    private static TelegramButtonMarkup BackToProjectsButtons() =>
        Buttons(Row(Button("Back", TelegramCallbackData.ListProjects)));

    private static TelegramButtonMarkup ProjectButtons(ProjectId projectId) =>
        Buttons(Row(Button("Back", TelegramCallbackData.ViewProject(projectId))));

    private static TelegramButtonMarkup TaskMenuButtons() =>
        Buttons(Row(Button("Back", TelegramCallbackData.TaskMenu)));

    private static TelegramButtonMarkup CancelDraftButtons() =>
        Buttons(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)));

    private static TelegramButtonMarkup ContinueDraftButtons() =>
        Buttons(Row(Button("Cancel", TelegramCallbackData.CancelContinueTask)));

    private static TelegramResponse RenderSettings(
        TelegramRunnerSettings settings,
        string? notice = null)
    {
        var sandboxEnabled = settings.CodexSandboxMode != "danger-full-access";
        var sandboxTarget = sandboxEnabled ? "danger-full-access" : "workspace-write";
        var bypassTarget = !settings.CodexBypassApprovalsAndSandbox;
        var noticeText = string.IsNullOrWhiteSpace(notice) ? "" : $"\n\n{notice}";

        return new TelegramResponse(
            $"""
            Settings

            Codex sandbox: {settings.CodexSandboxMode}
            Codex bypass approvals and sandbox: {(settings.CodexBypassApprovalsAndSandbox ? "allowed" : "disallowed")}
            {noticeText}
            """,
            Buttons(
                Row(Button(
                    sandboxEnabled ? "Sandbox enabled" : "Sandbox disabled",
                    TelegramCallbackData.SetCodexSandboxMode(sandboxTarget),
                    sandboxEnabled ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger),
                    Button(
                    settings.CodexBypassApprovalsAndSandbox ? "Bypass enabled" : "Bypass disabled",
                    TelegramCallbackData.SetCodexBypassApprovalsAndSandbox(bypassTarget),
                    settings.CodexBypassApprovalsAndSandbox ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger)),
                Row(Button("Back", TelegramCallbackData.MainMenu))));
    }

    private static TelegramButtonMarkup TaskDetailButtons(RuntimeTask task)
    {
        if (task.Status.IsTerminal())
        {
            return BackButtons();
        }

        if (task.Status == RuntimeTaskStatus.Reviewing)
        {
            return Buttons(
                Row(
                    Button("Continue", TelegramCallbackData.ContinueTask(task.Id), TelegramButtonStyle.Primary),
                    Button("Complete", TelegramCallbackData.CompleteTask(task.Id))),
                Row(
                    Button("Cancel", TelegramCallbackData.CancelTask(task.Id)),
                    Button("Back", TelegramCallbackData.MainMenu)));
        }

        return Buttons(
            Row(
                Button("Cancel", TelegramCallbackData.CancelTask(task.Id)),
                Button("Back", TelegramCallbackData.MainMenu)));
    }

    private static string FormatIteration(TaskIteration? iteration) =>
        iteration is null
            ? "(none)"
            : $"{iteration.IterationNumber} {iteration.Status.ToStorageValue()} via {iteration.RunnerId}";

    private static string FormatRunnerExecution(RuntimeRunnerExecution? execution)
    {
        if (execution is null)
        {
            return "(none)";
        }

        var status = execution.Cancelled
            ? "cancelled"
            : execution.TimedOut
                ? "timed out"
                : execution.ExitCode is null
                    ? "running"
                    : $"exit {execution.ExitCode}";

        return $"{execution.RunnerId} {status}";
    }

    private static string FormatArtifactPath(
        string artifactRootPath,
        string relativePath) =>
        Path.Combine(artifactRootPath, relativePath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static string FormatLastSeen(DateTimeOffset? lastSeenAt) =>
        lastSeenAt is null ? "never" : lastSeenAt.Value.ToString("O");

    private static string CountBadge(int count, int limit) =>
        count >= limit ? $"{limit}+" : count.ToString();

    private static string FormatStatus(RuntimeTaskStatus status) =>
        status.ToStorageValue().Replace('_', ' ');

    private static string ProjectButtonText(RuntimeProject project) =>
        project.IsArchived ? $"{project.Name} (archived)" : project.Name;

    private static TelegramButton Button(
        string text,
        string callbackData,
        TelegramButtonStyle style = TelegramButtonStyle.Default) =>
        new(text, callbackData, style);

    private static IReadOnlyList<TelegramButton> Row(params TelegramButton[] buttons) =>
        buttons;

    private static IReadOnlyList<TelegramButton>[] Grid(IEnumerable<TelegramButton> buttons) =>
        buttons.Chunk(ButtonGridColumns).Select(static chunk => Row(chunk)).ToArray();

    private static TelegramButtonMarkup Buttons(params IReadOnlyList<TelegramButton>[] rows) =>
        new(rows);

    private static RuntimeTaskStatus[] TaskStatuses { get; } =
    [
        RuntimeTaskStatus.Queued,
        RuntimeTaskStatus.Planning,
        RuntimeTaskStatus.Running,
        RuntimeTaskStatus.Reviewing,
        RuntimeTaskStatus.WaitingApproval,
        RuntimeTaskStatus.Completed,
        RuntimeTaskStatus.Failed,
        RuntimeTaskStatus.Cancelled,
    ];

    private sealed record TaskDraft(
        ProjectId ProjectId,
        MachineId? MachineId,
        string? Title,
        TaskDraftStep Step);

    private enum TaskDraftStep
    {
        ChoosingMachine,
        AwaitingTitle,
        AwaitingGoal,
    }
}
