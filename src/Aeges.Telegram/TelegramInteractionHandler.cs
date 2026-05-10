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
            TelegramCallbackData.ListPendingApprovals => await ListPendingApprovalsAsync(cancellationToken),
            TelegramCallbackData.CreateTask => await StartTaskCreationAsync(update.ChatId, cancellationToken),
            TelegramCallbackData.CancelCreateTask => CancelTaskCreation(update.ChatId),
            _ when TelegramCallbackData.TryParseSelectTaskProject(callbackData, out var projectId) =>
                await SelectTaskProjectAsync(update.ChatId, projectId, cancellationToken),
            _ when TelegramCallbackData.TryParseSelectTaskMachine(callbackData, out var machineId) =>
                await SelectTaskMachineAsync(update.ChatId, machineId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewTask(callbackData, out var taskId) =>
                await ViewTaskAsync(taskId, cancellationToken),
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
                    Button($"Queued tasks ({CountBadge(queuedTasks.Count, MenuCountLimit)})", TelegramCallbackData.ListQueuedTasks),
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

        if (draft.Step != TaskDraftStep.AwaitingGoal || draft.Title is null)
        {
            drafts.TryRemove(update.ChatId, out _);
            return await MainMenuAsync(cancellationToken);
        }

        var result = await application.CreateTaskAsync(
            draft.ProjectId,
            draft.MachineId,
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
            """,
            Buttons(
                Row(Button("View task", TelegramCallbackData.ViewTask(result.Value.Id))),
                Row(Button("Back", TelegramCallbackData.MainMenu))));
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

        drafts[chatId] = new TaskDraft(projectId, default, null, TaskDraftStep.ChoosingMachine);
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
        var tasks = await application.ListQueuedTasksAsync(DefaultTaskLimit, cancellationToken);

        if (tasks.Count == 0)
        {
            return new TelegramResponse("No queued tasks.", BackButtons());
        }

        var buttons = tasks
            .Select(task => Row(Button(task.Title, TelegramCallbackData.ViewTask(task.Id))))
            .Append(Row(Button("Back", TelegramCallbackData.MainMenu)))
            .ToArray();

        return new TelegramResponse(
            "Queued tasks:\n" + string.Join('\n', tasks.Select(task => $"- {task.Id}: {task.Title}")),
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

    private async Task<TelegramResponse> ViewTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        var buttons = task.Value!.Status.IsTerminal()
            ? BackButtons()
            : Buttons(
                Row(Button("Cancel", TelegramCallbackData.CancelTask(task.Value.Id))),
                Row(Button("Back", TelegramCallbackData.MainMenu)));

        return new TelegramResponse(
            $"""
            Task: {task.Value.Id}
            Title: {task.Value.Title}
            Status: {task.Value.Status.ToStorageValue()}
            Iterations: {task.Value.CurrentIteration}/{task.Value.MaxIterations}

            Goal:
            {task.Value.Goal}
            """,
            buttons);
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
            {value.Reason}
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

    private static TelegramButtonMarkup CancelDraftButtons() =>
        Buttons(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)));

    private static string FormatLastSeen(DateTimeOffset? lastSeenAt) =>
        lastSeenAt is null ? "never" : lastSeenAt.Value.ToString("O");

    private static string CountBadge(int count, int limit) =>
        count >= limit ? $"{limit}+" : count.ToString();

    private static TelegramButton Button(string text, string callbackData) =>
        new(text, callbackData);

    private static IReadOnlyList<TelegramButton> Row(params TelegramButton[] buttons) =>
        buttons;

    private static TelegramButtonMarkup Buttons(params IReadOnlyList<TelegramButton>[] rows) =>
        new(rows);

    private sealed record TaskDraft(
        ProjectId ProjectId,
        MachineId MachineId,
        string? Title,
        TaskDraftStep Step);

    private enum TaskDraftStep
    {
        ChoosingMachine,
        AwaitingTitle,
        AwaitingGoal,
    }
}
