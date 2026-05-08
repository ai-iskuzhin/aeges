using Aeges.Application.Configuration;
using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Handles Telegram interactions using button callback payloads.
/// </summary>
public sealed class TelegramInteractionHandler
{
    private const int DefaultTaskLimit = 10;
    private const int DefaultApprovalLimit = 10;
    private readonly ITelegramApplicationFacade application;
    private readonly HashSet<long> allowedChatIds;

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

        return callbackData switch
        {
            null or "" or TelegramCallbackData.MainMenu => MainMenu(),
            TelegramCallbackData.ListProjects => await ListProjectsAsync(cancellationToken),
            TelegramCallbackData.ListMachines => await ListMachinesAsync(cancellationToken),
            TelegramCallbackData.ListQueuedTasks => await ListQueuedTasksAsync(cancellationToken),
            TelegramCallbackData.ListPendingApprovals => await ListPendingApprovalsAsync(cancellationToken),
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

    private static TelegramResponse MainMenu() =>
        new(
            "Aeges control",
            Buttons(
                Row(Button("Projects", TelegramCallbackData.ListProjects), Button("Machines", TelegramCallbackData.ListMachines)),
                Row(Button("Queued tasks", TelegramCallbackData.ListQueuedTasks), Button("Approvals", TelegramCallbackData.ListPendingApprovals))));

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
                machines.Select(machine => $"- {machine.Id}: {machine.Name} ({machine.Status.ToStorageValue()})"));

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
        new("Unknown action. Choose an Aeges action below.", MainMenu().Buttons);

    private static TelegramButtonMarkup BackButtons() =>
        Buttons(Row(Button("Back", TelegramCallbackData.MainMenu)));

    private static TelegramButton Button(string text, string callbackData) =>
        new(text, callbackData);

    private static IReadOnlyList<TelegramButton> Row(params TelegramButton[] buttons) =>
        buttons;

    private static TelegramButtonMarkup Buttons(params IReadOnlyList<TelegramButton>[] rows) =>
        new(rows);
}
