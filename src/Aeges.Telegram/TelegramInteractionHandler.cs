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
    private readonly ITelegramApplicationFacade application;
    private readonly HashSet<long> allowedChatIds;
    private readonly ConcurrentDictionary<long, TaskDraft> drafts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramInteractionHandler"/> class.
    /// </summary>
    /// <param name="application">The Telegram application facade.</param>
    /// <param name="configuration">The Telegram configuration.</param>
    public TelegramInteractionHandler(
        ITelegramApplicationFacade application,
        AegesTelegramConfiguration configuration)
    {
        this.application = application;
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

        var callbackData = update.CallbackData?.Trim();

        if (callbackData is null or "")
        {
            return await HandleTextAsync(update, cancellationToken);
        }

        return callbackData switch
        {
            TelegramCallbackData.MainMenu => await MainMenuAsync(cancellationToken),
            TelegramCallbackData.ListProjects => await ListProjectsAsync(cancellationToken),
            TelegramCallbackData.ListMachines => await ListMachinesAsync(cancellationToken),
            TelegramCallbackData.ListQueuedTasks => await ListQueuedTasksAsync(cancellationToken),
            TelegramCallbackData.TaskMenu => await TaskMenuAsync(cancellationToken),
            TelegramCallbackData.ListPendingApprovals => await ListPendingApprovalsAsync(cancellationToken),
            TelegramCallbackData.CreateTask => await StartTaskCreationAsync(update.ChatId, cancellationToken),
            TelegramCallbackData.CancelCreateTask => CancelTaskCreation(update.ChatId),
            _ when TelegramCallbackData.TryParseListTasksByStatus(callbackData, out var status) =>
                await ListTasksByStatusAsync(status, cancellationToken),
            _ when TelegramCallbackData.TryParseSelectTaskProject(callbackData, out var projectId) =>
                await SelectTaskProjectAsync(update.ChatId, projectId, cancellationToken),
            _ when TelegramCallbackData.TryParseSelectTaskMachine(callbackData, out var machineId) =>
                await SelectTaskMachineAsync(update.ChatId, machineId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewTask(callbackData, out var taskId) =>
                await ViewTaskAsync(taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseCompleteTask(callbackData, out var taskId) =>
                await CompleteTaskAsync(taskId, cancellationToken),
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
    }

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
                Row(Button("New task", TelegramCallbackData.CreateTask)),
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
        if (!drafts.TryGetValue(update.ChatId, out var draft))
        {
            return await MainMenuAsync(cancellationToken);
        }

        var text = update.Text?.Trim();

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

        return new TelegramResponse(
            $"""
            Task queued: {result.Value!.Id}
            Project: {result.Value.ProjectId}
            Machine: {result.Value.MachineId}
            Title: {result.Value.Title}

            Goal:
            {TelegramMarkdown.Quote(text)}
            """,
            Buttons(
            Row(Button("View task", TelegramCallbackData.ViewTask(result.Value.Id))),
                Row(Button("Back", TelegramCallbackData.MainMenu))),
            new TelegramResponseMetadata(TelegramResponseKind.TaskWatch, result.Value.Id));
    }

    private async Task<TelegramResponse> StartTaskCreationAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        var projects = await application.ListProjectsAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return new TelegramResponse("No projects are registered. Add a project from the CLI first.", BackButtons());
        }

        drafts.TryRemove(chatId, out _);
        var rows = projects
            .Select(project => Row(Button(project.Name, TelegramCallbackData.SelectTaskProject(project.Id))))
            .Append(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)))
            .ToArray();

        return new TelegramResponse("Choose a project for the task.", Buttons(rows));
    }

    private async Task<TelegramResponse> SelectTaskProjectAsync(
        long chatId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var projects = await application.ListProjectsAsync(cancellationToken);

        if (!projects.Any(project => project.Id == projectId))
        {
            return new TelegramResponse($"Project '{projectId}' was not found.", BackButtons());
        }

        var machines = await application.ListMachinesAsync(cancellationToken);

        if (machines.Count == 0)
        {
            return new TelegramResponse("No machines are registered. Add a machine from the CLI first.", BackButtons());
        }

        drafts[chatId] = new TaskDraft(projectId, MachineId: null, null, TaskDraftStep.ChoosingMachine);
        var rows = machines
            .Select(machine => Row(Button($"{machine.Name} ({machine.Status.ToStorageValue()})", TelegramCallbackData.SelectTaskMachine(machine.Id))))
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

    private async Task<TelegramResponse> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = await application.ListProjectsAsync(cancellationToken);
        var text = projects.Count == 0
            ? "No projects are registered."
            : "Projects:\n" + string.Join('\n', projects.Select(project => $"- {project.Id}: {project.Name}"));

        return new TelegramResponse(text, BackButtons());
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
        var rows = new List<IReadOnlyList<TelegramButton>>();

        foreach (var status in TaskStatuses)
        {
            var tasks = await application.ListTasksByStatusAsync(status, MenuCountLimit, cancellationToken);
            rows.Add(Row(Button(
                $"{FormatStatus(status)} ({CountBadge(tasks.Count, MenuCountLimit)})",
                TelegramCallbackData.ListTasksByStatus(status))));
        }

        rows.Add(Row(Button("Back", TelegramCallbackData.MainMenu)));

        return new TelegramResponse("Tasks by status", Buttons([.. rows]));
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

        var buttons = tasks
            .Select(task => Row(Button(task.Title, TelegramCallbackData.ViewTask(task.Id))))
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

        var buttons = approvals
            .Select(approval => Row(Button(approval.Id.Value, TelegramCallbackData.ViewApproval(approval.Id))))
            .Append(Row(Button("Back", TelegramCallbackData.MainMenu)))
            .ToArray();

        return new TelegramResponse(
            "Pending approvals:\n" + string.Join(
                '\n',
                approvals.Select(approval => $"- {approval.Id}: {approval.RequestedAction}")),
            Buttons(buttons));
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
                    .Select(artifact => $"- {artifact.Type.ToStorageValue()}: {artifact.RelativePath}"));
        var runnerResponse = snapshot.LatestRunnerResponse is null
            ? "(none yet)"
            : snapshot.LatestRunnerResponse;

        return new TelegramResponse(
            $"""
            Task: {task.Id}
            Title: {task.Title}
            Status: {task.Status.ToStorageValue()}
            Iterations: {task.CurrentIteration}/{task.MaxIterations}
            Latest iteration: {FormatIteration(latestIteration)}
            Runner: {FormatRunnerExecution(latestExecution)}

            Goal:
            {TelegramMarkdown.Quote(task.Goal)}

            Runner response:
            {TelegramMarkdown.Quote(runnerResponse)}

            Artifacts:
            {artifactLines}
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

        return new TelegramResponse(
            $"Task completed: {task.Value!.Id}",
            Buttons(Row(Button("View task", TelegramCallbackData.ViewTask(task.Value.Id))), Row(Button("Back", TelegramCallbackData.MainMenu))),
            new TelegramResponseMetadata(TelegramResponseKind.TaskWatch, task.Value.Id));
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

    private static TelegramButtonMarkup BackButtons() =>
        Buttons(Row(Button("Back", TelegramCallbackData.MainMenu)));

    private static TelegramButtonMarkup TaskMenuButtons() =>
        Buttons(Row(Button("Back", TelegramCallbackData.TaskMenu)));

    private static TelegramButtonMarkup CancelDraftButtons() =>
        Buttons(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)));

    private static TelegramButtonMarkup TaskDetailButtons(RuntimeTask task)
    {
        if (task.Status.IsTerminal())
        {
            return BackButtons();
        }

        if (task.Status == RuntimeTaskStatus.Reviewing)
        {
            return Buttons(
                Row(Button("Complete", TelegramCallbackData.CompleteTask(task.Id))),
                Row(Button("Cancel", TelegramCallbackData.CancelTask(task.Id))),
                Row(Button("Back", TelegramCallbackData.MainMenu)));
        }

        return Buttons(
            Row(Button("Cancel", TelegramCallbackData.CancelTask(task.Id))),
            Row(Button("Back", TelegramCallbackData.MainMenu)));
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

    private static string FormatLastSeen(DateTimeOffset? lastSeenAt) =>
        lastSeenAt is null ? "never" : lastSeenAt.Value.ToString("O");

    private static string CountBadge(int count, int limit) =>
        count >= limit ? $"{limit}+" : count.ToString();

    private static string FormatStatus(RuntimeTaskStatus status) =>
        status.ToStorageValue().Replace('_', ' ');

    private static TelegramButton Button(string text, string callbackData) =>
        new(text, callbackData);

    private static IReadOnlyList<TelegramButton> Row(params TelegramButton[] buttons) =>
        buttons;

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
