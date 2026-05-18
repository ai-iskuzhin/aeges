using Aeges.Application;
using Aeges.Application.Configuration;
using Aeges.Application.TelegramUsers;
using Aeges.Core;
using System.Collections.Concurrent;
using System.Globalization;

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
    private const int WideProjectButtonTextLength = 15;
    private const int TaskGoalPreviewLength = 900;
    private const int TaskFollowUpPreviewLength = 900;
    private const int TaskResultPreviewLength = 2600;
    private const int TaskProgressLimit = 20;
    private const int TaskProgressMessageLength = 220;
    private const int TaskArtifactsLimit = 25;
    private const int TaskArtifactPathLength = 260;
    private readonly ITelegramApplicationFacade application;
    private readonly ITelegramCallbackRegistry? callbackRegistry;
    private readonly string? runtimeVersion;
    private readonly AegesTelegramConfiguration configuration;
    private readonly HashSet<long> allowedChatIds;
    private readonly ConcurrentDictionary<TelegramConversationKey, TaskDraft> drafts = new();
    private readonly ConcurrentDictionary<TelegramConversationKey, TaskContinuationDraft> continuationDrafts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramInteractionHandler"/> class.
    /// </summary>
    /// <param name="application">The Telegram application facade.</param>
    /// <param name="configuration">The Telegram configuration.</param>
    /// <param name="callbackRegistry">The optional callback registry used to shorten Telegram callback payloads.</param>
    /// <param name="runtimeVersion">The optional Aeges runtime version shown in Telegram menus.</param>
    public TelegramInteractionHandler(
        ITelegramApplicationFacade application,
        AegesTelegramConfiguration configuration,
        ITelegramCallbackRegistry? callbackRegistry = null,
        string? runtimeVersion = null)
    {
        this.application = application;
        this.callbackRegistry = callbackRegistry;
        this.runtimeVersion = string.IsNullOrWhiteSpace(runtimeVersion) ? null : runtimeVersion.Trim();
        this.configuration = configuration;
        allowedChatIds = [.. configuration.AllowedChatIds];
    }

    /// <summary>
    /// Gets a value indicating whether private Telegram message thread identifiers are part of routing.
    /// </summary>
    public bool IsPrivateChatThreadRoutingEnabled => configuration.EnablePrivateChatThreads;

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
        if (!IsOuterAuthorized(update.ChatId))
        {
            return new TelegramResponse(
                "This Telegram chat is not authorized for Aeges.",
                TelegramButtonMarkup.Empty);
        }

        var authorization = await application.EnsureTelegramUserAsync(
            update.AuthorizationUserId,
            update.ToUserProfile(),
            cancellationToken);

        if (!authorization.IsApproved)
        {
            return RenderUnauthorizedUser(authorization.User);
        }

        var callbackData = await ResolveCallbackDataAsync(update.ChatId, update.CallbackData?.Trim(), cancellationToken);

        if (callbackData is null or "")
        {
            return await TokenizeResponseAsync(
                update.ChatId,
                await HandleTextAsync(update, authorization, cancellationToken),
                cancellationToken);
        }

        var response = callbackData switch
        {
            TelegramCallbackData.MainMenu => await MainMenuAsync(authorization, cancellationToken),
            TelegramCallbackData.ListProjects => await ListProjectsAsync(authorization, cancellationToken),
            TelegramCallbackData.ListMachines => await ListMachinesAsync(cancellationToken),
            TelegramCallbackData.ListQueuedTasks => await ListQueuedTasksAsync(authorization, cancellationToken),
            TelegramCallbackData.TaskMenu => await TaskMenuAsync(authorization, cancellationToken),
            TelegramCallbackData.ListPendingApprovals => await RequireAdmin(authorization, () => ListPendingApprovalsAsync(cancellationToken)),
            TelegramCallbackData.SettingsMenu => await RequireAdmin(authorization, () => SettingsMenuAsync(cancellationToken)),
            TelegramCallbackData.ParallelTasksSettingsMenu => await RequireAdmin(authorization, () => ParallelTasksSettingsMenuAsync(cancellationToken)),
            TelegramCallbackData.UserMenu => await RequireAdmin(authorization, () => ListTelegramUsersAsync(cancellationToken)),
            TelegramCallbackData.CancelPendingTextResponse => PendingTextResponseAlreadyFinished(),
            TelegramCallbackData.CreateTask => await StartTaskCreationAsync(update, authorization, cancellationToken),
            TelegramCallbackData.CancelCreateTask => CancelTaskCreation(update),
            TelegramCallbackData.CancelContinueTask => CancelTaskContinuation(update),
            _ when TelegramCallbackData.TryParseSetCodexSandboxMode(callbackData, out var sandboxMode) =>
                await RequireAdmin(authorization, () => SetCodexSandboxModeAsync(sandboxMode, cancellationToken)),
            _ when TelegramCallbackData.TryParseSetCodexBypassApprovalsAndSandbox(callbackData, out var bypassEnabled) =>
                await RequireAdmin(authorization, () => SetCodexBypassApprovalsAndSandboxAsync(bypassEnabled, cancellationToken)),
            _ when TelegramCallbackData.TryParseSetAgentMaxParallelTasks(callbackData, out var maxParallelTasks) =>
                await RequireAdmin(authorization, () => SetAgentMaxParallelTasksAsync(maxParallelTasks, cancellationToken)),
            _ when TelegramCallbackData.TryParseSetPrivateChatThreads(callbackData, out var privateChatThreadsEnabled) =>
                await RequireAdmin(authorization, () => SetPrivateChatThreadsAsync(privateChatThreadsEnabled, cancellationToken)),
            _ when TelegramCallbackData.TryParseListTasksByStatus(callbackData, out var status) =>
                await ListTasksByStatusAsync(authorization, status, cancellationToken),
            _ when TelegramCallbackData.TryParseListProjectTasksByStatus(callbackData, out var projectId, out var status) =>
                await ListProjectTasksByStatusAsync(authorization, projectId, status, cancellationToken),
            TelegramCallbackData.ViewUngroupedProjects => await ListProjectGroupProjectsAsync(authorization, null, cancellationToken),
            _ when TelegramCallbackData.TryParseViewProjectGroup(callbackData, out var groupId) =>
                await ListProjectGroupProjectsAsync(authorization, groupId, cancellationToken),
            _ when TelegramCallbackData.TryParseConfirmArchiveProject(callbackData, out var projectId) =>
                await RequireAdmin(authorization, () => ConfirmArchiveProjectAsync(projectId, cancellationToken)),
            _ when TelegramCallbackData.TryParseArchiveProject(callbackData, out var projectId) =>
                await RequireAdmin(authorization, () => ArchiveProjectAsync(projectId, cancellationToken)),
            _ when TelegramCallbackData.TryParseViewProject(callbackData, out var projectId) =>
                await ViewProjectAsync(authorization, projectId, cancellationToken),
            _ when TelegramCallbackData.TryParseSelectTaskProject(callbackData, out var projectId) =>
                await SelectTaskProjectAsync(update, authorization, projectId, cancellationToken),
            _ when TelegramCallbackData.TryParseSelectTaskMachine(callbackData, out var machineId) =>
                await SelectTaskMachineAsync(update, machineId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewTask(callbackData, out var taskId) =>
                await ViewTaskAsync(authorization, taskId, update.MessageThreadId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewTaskResult(callbackData, out var taskId) =>
                await ViewTaskResultAsync(authorization, taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewTaskProgress(callbackData, out var taskId) =>
                await ViewTaskProgressAsync(authorization, taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseViewTaskArtifacts(callbackData, out var taskId) =>
                await ViewTaskArtifactsAsync(authorization, taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseCompleteTask(callbackData, out var taskId) =>
                await CompleteTaskAsync(authorization, taskId, update.MessageThreadId, cancellationToken),
            _ when TelegramCallbackData.TryParseContinueTask(callbackData, out var taskId) =>
                await StartTaskContinuationAsync(update, authorization, taskId, cancellationToken),
            _ when TelegramCallbackData.TryParseCancelTask(callbackData, out var taskId) =>
                await CancelTaskAsync(authorization, taskId, update.ChatId, update.MessageThreadId, cancellationToken),
            _ when TelegramCallbackData.TryParseApproveApproval(callbackData, out var approvalId) =>
                await RequireAdmin(authorization, () => ResolveApprovalAsync(approvalId, approved: true, update.ChatId, cancellationToken)),
            _ when TelegramCallbackData.TryParseRejectApproval(callbackData, out var approvalId) =>
                await RequireAdmin(authorization, () => ResolveApprovalAsync(approvalId, approved: false, update.ChatId, cancellationToken)),
            _ when TelegramCallbackData.TryParseViewApproval(callbackData, out var approvalId) =>
                await RequireAdmin(authorization, () => ViewApprovalAsync(approvalId, cancellationToken)),
            _ when TelegramCallbackData.TryParseViewTelegramUser(callbackData, out var userId) =>
                await RequireAdmin(authorization, () => ViewTelegramUserAsync(userId, authorization.User.Id, cancellationToken)),
            _ when TelegramCallbackData.TryParseApproveTelegramUser(callbackData, out var userId) =>
                await RequireAdmin(authorization, () => ApproveTelegramUserAsync(userId, cancellationToken)),
            _ when TelegramCallbackData.TryParseDenyTelegramUser(callbackData, out var userId) =>
                await RequireAdmin(authorization, () => DenyTelegramUserAsync(userId, authorization.User.Id, cancellationToken)),
            _ when TelegramCallbackData.TryParseSetTelegramProjectAccess(callbackData, out var userId, out var projectId, out var allowed) =>
                await RequireAdmin(authorization, () => SetTelegramProjectAccessAsync(userId, projectId, allowed, cancellationToken)),
            _ when TelegramCallbackData.TryParseSetTelegramProjectGroupAccess(callbackData, out var userId, out var groupId, out var allowed) =>
                await RequireAdmin(authorization, () => SetTelegramProjectGroupAccessAsync(userId, groupId, allowed, cancellationToken)),
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

    /// <summary>
    /// Resolves a transport callback payload into its logical callback data.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="callbackData">The raw callback payload from Telegram.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The logical callback data, or the original value when no registry is configured.</returns>
    public async Task<string?> ResolveCallbackDataForTransportAsync(
        long chatId,
        string? callbackData,
        CancellationToken cancellationToken) =>
        await ResolveCallbackDataAsync(chatId, callbackData?.Trim(), cancellationToken);

    /// <summary>
    /// Records where Telegram should route updates for a task.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="messageThreadId">The Telegram forum topic identifier, when available.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="detailMessageId">The latest editable task details message, when available.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RecordTaskBindingAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        int? detailMessageId,
        CancellationToken cancellationToken) =>
        await application.RecordTaskBindingAsync(chatId, messageThreadId, taskId, detailMessageId, cancellationToken);

    /// <summary>
    /// Lists durable Telegram task routing bindings.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The durable Telegram task routing bindings.</returns>
    public async Task<IReadOnlyList<RuntimeTelegramTaskBinding>> ListTaskBindingsAsync(CancellationToken cancellationToken) =>
        await application.ListTaskBindingsAsync(cancellationToken);

    /// <summary>
    /// Forgets a Telegram task routing binding.
    /// </summary>
    /// <param name="chatId">The Telegram chat identifier.</param>
    /// <param name="messageThreadId">The Telegram forum topic identifier, when available.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ForgetTaskBindingAsync(
        long chatId,
        int? messageThreadId,
        TaskId taskId,
        CancellationToken cancellationToken) =>
        await application.ForgetTaskBindingAsync(chatId, messageThreadId, taskId, cancellationToken);

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

    /// <summary>
    /// Determines whether an inbound text message is the Telegram start command.
    /// </summary>
    /// <param name="text">The inbound text.</param>
    /// <returns><see langword="true"/> when the text requests the main menu.</returns>
    public static bool IsStartCommand(string? text)
    {
        var trimmed = text?.Trim();

        return trimmed is "/start"
            || (trimmed?.StartsWith("/start@", StringComparison.OrdinalIgnoreCase) ?? false)
            || (trimmed?.StartsWith("/start ", StringComparison.Ordinal) ?? false);
    }

    private bool IsOuterAuthorized(long chatId) =>
        allowedChatIds.Count == 0 || allowedChatIds.Contains(chatId);

    private static TelegramResponse RenderUnauthorizedUser(RuntimeTelegramUser user)
    {
        var status = user.Status.ToStorageValue();
        var message = user.Status == TelegramUserStatus.Pending
            ? "Your access request is waiting for an Aeges Telegram administrator."
            : "Your Telegram chat is denied access to Aeges.";

        return new TelegramResponse(
            $"{message}\n{TelegramMarkdown.Quote(FormatTelegramUserMetadata(user, includeGrants: false, 0, 0))}",
            TelegramButtonMarkup.Empty);
    }

    private static async Task<TelegramResponse> RequireAdmin(
        TelegramUserAuthorization authorization,
        Func<Task<TelegramResponse>> action) =>
        authorization.IsAdmin
            ? await action()
            : new TelegramResponse("Only Telegram administrators can use this action.", BackButtons());

    private async Task<TelegramResponse> MainMenuAsync(
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken,
        string? notice = null)
    {
        var projects = await ListAccessibleProjectsAsync(authorization, cancellationToken);
        var machines = await application.ListMachinesAsync(cancellationToken);
        var queuedTasks = await FilterTasksAsync(
            authorization,
            await application.ListQueuedTasksAsync(MenuCountLimit, cancellationToken),
            cancellationToken);
        var approvals = authorization.IsAdmin
            ? await application.ListPendingApprovalsAsync(MenuCountLimit, cancellationToken)
            : [];
        var users = authorization.IsAdmin
            ? await application.ListTelegramUsersAsync(cancellationToken)
            : [];
        var firstRow = authorization.IsAdmin
            ? Row(
                Button("New task", TelegramCallbackData.CreateTask),
                Button("Settings", TelegramCallbackData.SettingsMenu))
            : Row(Button("New task", TelegramCallbackData.CreateTask));
        var menuRows = new List<IReadOnlyList<TelegramButton>>
        {
            firstRow,
            Row(
                Button($"Projects ({projects.Count})", TelegramCallbackData.ListProjects),
                Button($"Machines ({machines.Count})", TelegramCallbackData.ListMachines)),
            Row(Button($"Tasks ({CountBadge(queuedTasks.Count, MenuCountLimit)} queued)", TelegramCallbackData.TaskMenu)),
        };

        if (authorization.IsAdmin)
        {
            menuRows.Add(Row(
                Button($"Approvals ({CountBadge(approvals.Count, MenuCountLimit)})", TelegramCallbackData.ListPendingApprovals),
                Button($"Users ({CountBadge(users.Count, MenuCountLimit)})", TelegramCallbackData.UserMenu)));
        }

        var menuText = runtimeVersion is null ? "Aeges control" : $"Aeges control\nVersion: {runtimeVersion}";

        return new TelegramResponse(
            string.IsNullOrWhiteSpace(notice) ? menuText : $"{notice}\n\n{menuText}",
            Buttons([.. menuRows]));
    }

    private async Task<TelegramResponse> HandleTextAsync(
        TelegramUpdate update,
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var text = update.Text?.Trim();
        var conversation = GetConversationKey(update);

        if (IsStartCommand(text))
        {
            drafts.TryRemove(conversation, out _);
            continuationDrafts.TryRemove(conversation, out _);

            return await MainMenuAsync(authorization, cancellationToken);
        }

        if (continuationDrafts.TryGetValue(conversation, out var continuationDraft))
        {
            var validation = ValidateContinuationDraft(update, continuationDraft);
            if (validation is not null)
            {
                return validation;
            }

            return string.IsNullOrWhiteSpace(text)
                ? new TelegramResponse("Send non-empty feedback, or cancel task continuation.", ContinueDraftButtons())
                : await ContinueTaskWithFeedbackAsync(update, continuationDraft.TaskId, text, cancellationToken);
        }

        if (!drafts.TryGetValue(conversation, out var draft))
        {
            return await SendTalkMessageAsync(update, authorization, text, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TelegramResponse("Send non-empty text, or cancel task creation.", CancelDraftButtons());
        }

        if (draft.Step == TaskDraftStep.AwaitingTitle)
        {
            drafts[conversation] = draft with
            {
                Title = text,
                Step = TaskDraftStep.AwaitingGoal,
            };

            return new TelegramResponse("Now send the task goal/details.", CancelDraftButtons());
        }

        if (draft.Step != TaskDraftStep.AwaitingGoal || draft.Title is null || draft.MachineId is null)
        {
            drafts.TryRemove(conversation, out _);
            return await MainMenuAsync(authorization, cancellationToken);
        }

        var result = await application.CreateTaskAsync(
            draft.ProjectId,
            draft.MachineId.Value,
            draft.Title,
            text,
            cancellationToken);
        drafts.TryRemove(conversation, out _);

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
        TelegramUpdate update,
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var projects = await ListAccessibleActiveProjectsAsync(authorization, cancellationToken);

        if (projects.Count == 0)
        {
            return new TelegramResponse("No active projects are registered. Add a project from the CLI first.", BackButtons());
        }

        var conversation = GetConversationKey(update);
        drafts.TryRemove(conversation, out _);
        continuationDrafts.TryRemove(conversation, out _);
        var rows = ProjectGrid(projects.Select(project => Button(project.Name, TelegramCallbackData.SelectTaskProject(project.Id))))
            .Append(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)))
            .ToArray();

        return new TelegramResponse("Choose a project for the task.", Buttons(rows));
    }

    private async Task<TelegramResponse> SendTalkMessageAsync(
        TelegramUpdate update,
        TelegramUserAuthorization authorization,
        string? text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return await MainMenuAsync(authorization, cancellationToken);
        }

        var result = await application.SendTalkMessageAsync(CreateTalkSource(update), text, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse(
                $"{result.Error!.Code}: {result.Error.Message}",
                TelegramButtonMarkup.Empty);
        }

        var exchange = result.Value!;

        return new TelegramResponse(
            $"""
            {TelegramMarkdown.Quote($"Session: {exchange.Session.Id}\nRunner: {exchange.Session.RunnerId}")}

            {exchange.AssistantMessage.Content}
            """,
            TelegramButtonMarkup.Empty);
    }

    private async Task<TelegramResponse> SelectTaskProjectAsync(
        TelegramUpdate update,
        TelegramUserAuthorization authorization,
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

        if (!await CanAccessProjectAsync(authorization, project.Value, cancellationToken))
        {
            return new TelegramResponse("You do not have access to this project.", BackToProjectsButtons());
        }

        var machines = await application.ListMachinesAsync(cancellationToken);

        if (machines.Count == 0)
        {
            return new TelegramResponse("No machines are registered. Add a machine from the CLI first.", BackButtons());
        }

        drafts[GetConversationKey(update)] = new TaskDraft(projectId, MachineId: null, null, TaskDraftStep.ChoosingMachine);
        var rows = Grid(machines.Select(machine => Button(
                $"{machine.Name} ({machine.Status.ToStorageValue()})",
                TelegramCallbackData.SelectTaskMachine(machine.Id))))
            .Append(Row(Button("Cancel", TelegramCallbackData.CancelCreateTask)))
            .ToArray();

        return new TelegramResponse("Choose the machine that should process the task.", Buttons(rows));
    }

    private Task<TelegramResponse> SelectTaskMachineAsync(
        TelegramUpdate update,
        MachineId machineId,
        CancellationToken cancellationToken)
    {
        var conversation = GetConversationKey(update);
        if (!drafts.TryGetValue(conversation, out var draft) || draft.Step != TaskDraftStep.ChoosingMachine)
        {
            return Task.FromResult(new TelegramResponse("Start task creation first.", BackButtons()));
        }

        drafts[conversation] = draft with
        {
            MachineId = machineId,
            Step = TaskDraftStep.AwaitingTitle,
        };

        return Task.FromResult(new TelegramResponse("Send the task title.", CancelDraftButtons()));
    }

    private TelegramResponse CancelTaskCreation(TelegramUpdate update)
    {
        drafts.TryRemove(GetConversationKey(update), out _);

        return new TelegramResponse("Task creation cancelled.", BackButtons());
    }

    private async Task<TelegramResponse> StartTaskContinuationAsync(
        TelegramUpdate update,
        TelegramUserAuthorization authorization,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        if (!await CanAccessProjectAsync(authorization, task.Value!.ProjectId, cancellationToken))
        {
            return new TelegramResponse("You do not have access to this task.", TaskMenuButtons());
        }

        if (!CanContinueTask(task.Value.Status))
        {
            return new TelegramResponse("Only reviewing or cancelled tasks can be continued.", BackButtons());
        }

        if (task.Value.CurrentIteration >= task.Value.MaxIterations)
        {
            return new TelegramResponse("This task has reached its iteration limit.", BackButtons());
        }

        var conversation = GetConversationKey(update);
        drafts.TryRemove(conversation, out _);
        continuationDrafts[conversation] = new TaskContinuationDraft(
            taskId,
            GetSenderScope(update),
            GetRoutingThreadId(update),
            update.MessageId,
            RequiresReplyToPrompt: !update.IsPrivateChat && update.MessageId is not null);

        return new TelegramResponse(
            update.IsPrivateChat
                ? "Send the follow-up instructions for the next iteration."
                : "Reply to this message with the follow-up instructions for the next iteration.",
            ContinueDraftButtons());
    }

    private TelegramResponse? ValidateContinuationDraft(
        TelegramUpdate update,
        TaskContinuationDraft draft)
    {
        if (draft.SenderUserId != GetSenderScope(update))
        {
            return new TelegramResponse(
                "Task continuation is waiting for the user who pressed Continue.",
                ContinueDraftButtons());
        }

        if (draft.MessageThreadId != GetRoutingThreadId(update))
        {
            return new TelegramResponse(
                "Task continuation is waiting in the original topic.",
                ContinueDraftButtons());
        }

        if (draft.RequiresReplyToPrompt && update.ReplyToMessageId != draft.PromptMessageId)
        {
            return new TelegramResponse(
                "Reply to the bot follow-up prompt to continue this task.",
                ContinueDraftButtons());
        }

        return null;
    }

    private async Task<TelegramResponse> ContinueTaskWithFeedbackAsync(
        TelegramUpdate update,
        TaskId taskId,
        string feedback,
        CancellationToken cancellationToken)
    {
        var result = await application.ContinueTaskAsync(taskId, feedback, cancellationToken);
        continuationDrafts.TryRemove(GetConversationKey(update), out _);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        await StartAgentNoticeAsync(cancellationToken);

        return await RenderTaskDetailsAsync(result.Value!.Id, cancellationToken, includeTerminalNavigation: update.MessageThreadId is null);
    }

    private TelegramResponse CancelTaskContinuation(TelegramUpdate update)
    {
        var conversation = GetConversationKey(update);
        if (continuationDrafts.TryGetValue(conversation, out var draft) && draft.SenderUserId != GetSenderScope(update))
        {
            return new TelegramResponse(
                "Only the user who pressed Continue can cancel this task continuation.",
                ContinueDraftButtons());
        }

        continuationDrafts.TryRemove(conversation, out _);

        return new TelegramResponse("Task continuation cancelled.", BackButtons());
    }

    private async Task<TelegramResponse> ListProjectsAsync(
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var projects = await ListAccessibleProjectsAsync(authorization, cancellationToken);
        var groups = await application.ListProjectGroupsAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return new TelegramResponse("No projects are registered.", BackButtons());
        }

        var activeGroups = groups
            .Where(group => !group.IsArchived)
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var groupedProjectCounts = projects
            .Where(project => project.GroupId is not null)
            .GroupBy(project => project.GroupId!.Value.Value)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var ungroupedCount = projects.Count(project => project.GroupId is null);
        var buttons = activeGroups
            .Select(group =>
            {
                groupedProjectCounts.TryGetValue(group.Id.Value, out var count);
                return Button($"{group.Name} ({count})", TelegramCallbackData.ViewProjectGroup(group.Id));
            })
            .ToList();

        if (ungroupedCount > 0)
        {
            buttons.Add(Button($"Ungrouped ({ungroupedCount})", TelegramCallbackData.ViewUngroupedProjects));
        }

        var rows = Grid(buttons)
            .Append(Row(Button("Back", TelegramCallbackData.MainMenu)))
            .ToArray();

        return new TelegramResponse("Projects", Buttons(rows));
    }

    private async Task<TelegramResponse> ListProjectGroupProjectsAsync(
        TelegramUserAuthorization authorization,
        ProjectGroupId? groupId,
        CancellationToken cancellationToken)
    {
        var projects = await ListAccessibleProjectsAsync(authorization, cancellationToken);
        var groups = await application.ListProjectGroupsAsync(cancellationToken);
        var group = groupId is null ? null : groups.FirstOrDefault(candidate => candidate.Id == groupId);

        if (groupId is not null && group is null)
        {
            return new TelegramResponse($"Project group '{groupId}' was not found.", BackToProjectsButtons());
        }

        var groupedProjects = projects
            .Where(project => project.GroupId == groupId)
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (groupedProjects.Length == 0)
        {
            return new TelegramResponse(
                groupId is null ? "No ungrouped projects are registered." : $"No projects are registered in {group!.Name}.",
                BackToProjectsButtons());
        }

        var rows = ProjectGrid(groupedProjects.Select(project => Button(ProjectButtonText(project), TelegramCallbackData.ViewProject(project.Id))))
            .Append(Row(Button("Back", TelegramCallbackData.ListProjects)))
            .ToArray();
        var title = groupId is null ? "Ungrouped projects" : $"{group!.Name} projects";

        return new TelegramResponse(title, Buttons(rows));
    }

    private async Task<TelegramResponse> ViewProjectAsync(
        TelegramUserAuthorization authorization,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = await application.GetProjectAsync(projectId, cancellationToken);

        if (!project.IsSuccess)
        {
            return new TelegramResponse($"{project.Error!.Code}: {project.Error.Message}", BackToProjectsButtons());
        }

        if (!await CanAccessProjectAsync(authorization, project.Value!, cancellationToken))
        {
            return new TelegramResponse("You do not have access to this project.", BackToProjectsButtons());
        }

        return await RenderProjectDetailsAsync(project.Value!, notice: null, cancellationToken);
    }

    private async Task<TelegramResponse> ArchiveProjectAsync(
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = await application.GetProjectAsync(projectId, cancellationToken);

        if (!project.IsSuccess)
        {
            return new TelegramResponse($"{project.Error!.Code}: {project.Error.Message}", BackToProjectsButtons());
        }

        if (project.Value!.IsArchived)
        {
            return await RenderProjectDetailsAsync(project.Value, "Project is already archived.", cancellationToken);
        }

        var metadata = string.Join(
            '\n',
            [
                $"Project: {project.Value.Id}",
                $"Name: {project.Value.Name}",
                $"Group: {project.Value.GroupId?.Value ?? "ungrouped"}",
                $"Path: {project.Value.Path}",
            ]);

        return new TelegramResponse(
            $"Archive this project?\n{TelegramMarkdown.Quote(metadata)}",
            Buttons(
                Row(
                    Button("Confirm archive", TelegramCallbackData.ConfirmArchiveProject(projectId), TelegramButtonStyle.Danger),
                    Button("Back", TelegramCallbackData.ViewProject(projectId)))));
    }

    private async Task<TelegramResponse> ConfirmArchiveProjectAsync(
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
            :
            [
                Button("New task", TelegramCallbackData.SelectTaskProject(project.Id)),
                Button("Archive", TelegramCallbackData.ArchiveProject(project.Id), TelegramButtonStyle.Danger),
            ];
        var rows = Grid(buttons.Concat(actionButtons))
            .Append(Row(Button("Back", TelegramCallbackData.ListProjects)))
            .ToArray();

        List<string> metadataLines =
        [
            $"Project: {project.Id}",
            $"Name: {project.Name}",
            $"Status: {(project.IsArchived ? "archived" : "active")}",
            $"Group: {project.GroupId?.Value ?? "ungrouped"}",
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
        TelegramUserAuthorization authorization,
        ProjectId projectId,
        RuntimeTaskStatus status,
        CancellationToken cancellationToken)
    {
        var project = await application.GetProjectAsync(projectId, cancellationToken);

        if (!project.IsSuccess)
        {
            return new TelegramResponse($"{project.Error!.Code}: {project.Error.Message}", BackToProjectsButtons());
        }

        if (!await CanAccessProjectAsync(authorization, project.Value!, cancellationToken))
        {
            return new TelegramResponse("You do not have access to this project.", BackToProjectsButtons());
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

    private async Task<TelegramResponse> ListTelegramUsersAsync(CancellationToken cancellationToken)
    {
        var users = await application.ListTelegramUsersAsync(cancellationToken);

        if (users.Count == 0)
        {
            return new TelegramResponse("No Telegram users are registered yet.", BackButtons());
        }

        var rows = Grid(users.Select(user => Button(
                $"{FormatTelegramUserStatus(user)} {FormatTelegramUserDisplay(user)}",
                TelegramCallbackData.ViewTelegramUser(user.Id),
                user.Status == TelegramUserStatus.Approved ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger)))
            .Append(Row(Button("Back", TelegramCallbackData.MainMenu)))
            .ToArray();

        return new TelegramResponse(
            "Telegram users:\n" + string.Join('\n', users.Select(user => $"- {FormatTelegramUserDisplay(user)}: {user.Role.ToStorageValue()} / {user.Status.ToStorageValue()}")),
            Buttons(rows));
    }

    private async Task<TelegramResponse> ViewTelegramUserAsync(
        TelegramUserId userId,
        TelegramUserId actingUserId,
        CancellationToken cancellationToken)
    {
        var access = await application.GetTelegramUserAccessAsync(userId, cancellationToken);

        if (!access.IsSuccess)
        {
            return new TelegramResponse($"{access.Error!.Code}: {access.Error.Message}", BackButtons());
        }

        return await RenderTelegramUserAccessAsync(access.Value!, null, actingUserId, cancellationToken);
    }

    private async Task<TelegramResponse> ApproveTelegramUserAsync(
        TelegramUserId userId,
        CancellationToken cancellationToken)
    {
        var result = await application.ApproveTelegramUserAsync(userId, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        var access = await application.GetTelegramUserAccessAsync(userId, cancellationToken);
        return access.IsSuccess
            ? await RenderTelegramUserAccessAsync(access.Value!, "User approved.", actingUserId: null, cancellationToken)
            : new TelegramResponse($"{access.Error!.Code}: {access.Error.Message}", BackButtons());
    }

    private async Task<TelegramResponse> DenyTelegramUserAsync(
        TelegramUserId userId,
        TelegramUserId actingUserId,
        CancellationToken cancellationToken)
    {
        if (userId == actingUserId)
        {
            var selfAccess = await application.GetTelegramUserAccessAsync(userId, cancellationToken);
            return selfAccess.IsSuccess
                ? await RenderTelegramUserAccessAsync(selfAccess.Value!, "You cannot deny your own Telegram user.", actingUserId, cancellationToken)
                : new TelegramResponse("You cannot deny your own Telegram user.", BackButtons());
        }

        var result = await application.DenyTelegramUserAsync(userId, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        var access = await application.GetTelegramUserAccessAsync(userId, cancellationToken);
        return access.IsSuccess
            ? await RenderTelegramUserAccessAsync(access.Value!, "User denied.", actingUserId: null, cancellationToken)
            : new TelegramResponse($"{access.Error!.Code}: {access.Error.Message}", BackButtons());
    }

    private async Task<TelegramResponse> SetTelegramProjectAccessAsync(
        TelegramUserId userId,
        ProjectId projectId,
        bool allowed,
        CancellationToken cancellationToken)
    {
        var result = await application.SetTelegramProjectAccessAsync(userId, projectId, allowed, cancellationToken);

        return result.IsSuccess
            ? await RenderTelegramUserAccessAsync(result.Value!, allowed ? "Project access granted." : "Project access revoked.", actingUserId: null, cancellationToken)
            : new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
    }

    private async Task<TelegramResponse> SetTelegramProjectGroupAccessAsync(
        TelegramUserId userId,
        ProjectGroupId groupId,
        bool allowed,
        CancellationToken cancellationToken)
    {
        var result = await application.SetTelegramProjectGroupAccessAsync(userId, groupId, allowed, cancellationToken);

        return result.IsSuccess
            ? await RenderTelegramUserAccessAsync(result.Value!, allowed ? "Group access granted." : "Group access revoked.", actingUserId: null, cancellationToken)
            : new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
    }

    private async Task<TelegramResponse> RenderTelegramUserAccessAsync(
        TelegramUserAccessSnapshot access,
        string? notice,
        TelegramUserId? actingUserId,
        CancellationToken cancellationToken)
    {
        var user = access.User;
        var allowedProjects = access.ProjectAccess.Select(project => project.ProjectId).ToHashSet();
        var allowedGroups = access.ProjectGroupAccess.Select(group => group.ProjectGroupId).ToHashSet();
        var groups = await application.ListProjectGroupsAsync(cancellationToken);
        var projects = await application.ListProjectsAsync(cancellationToken);
        var buttons = new List<TelegramButton>();

        if (user.Status != TelegramUserStatus.Approved)
        {
            buttons.Add(Button("Approve", TelegramCallbackData.ApproveTelegramUser(user.Id), TelegramButtonStyle.Success));
        }

        if (user.Status != TelegramUserStatus.Denied && user.Id != actingUserId)
        {
            buttons.Add(Button("Deny", TelegramCallbackData.DenyTelegramUser(user.Id), TelegramButtonStyle.Danger));
        }

        buttons.AddRange(groups
            .Where(group => !group.IsArchived)
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var allowed = allowedGroups.Contains(group.Id);
                return Button(
                    $"Group {group.Name}",
                    TelegramCallbackData.SetTelegramProjectGroupAccess(user.Id, group.Id, !allowed),
                    allowed ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger);
            }));
        buttons.AddRange(projects
            .Where(project => !project.IsArchived)
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .Select(project =>
            {
                var allowed = allowedProjects.Contains(project.Id);
                return Button(
                    project.Name,
                    TelegramCallbackData.SetTelegramProjectAccess(user.Id, project.Id, !allowed),
                    allowed ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger);
            }));

        var metadata = FormatTelegramUserMetadata(user, includeGrants: true, allowedProjects.Count, allowedGroups.Count);
        var rows = Grid(buttons)
            .Append(Row(Button("Back", TelegramCallbackData.UserMenu)))
            .ToArray();
        var prefix = string.IsNullOrWhiteSpace(notice)
            ? "Telegram user:"
            : $"{notice}\n\nTelegram user:";

        return new TelegramResponse($"{prefix}\n{TelegramMarkdown.Quote(metadata)}", Buttons(rows));
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

    private async Task<TelegramResponse> ListQueuedTasksAsync(
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken)
    {
        return await ListTasksByStatusAsync(authorization, RuntimeTaskStatus.Queued, cancellationToken);
    }

    private async Task<TelegramResponse> TaskMenuAsync(
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var buttons = new List<TelegramButton>();

        foreach (var status in TaskStatuses)
        {
            var tasks = await FilterTasksAsync(
                authorization,
                await application.ListTasksByStatusAsync(status, MenuCountLimit, cancellationToken),
                cancellationToken);
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
        TelegramUserAuthorization authorization,
        RuntimeTaskStatus status,
        CancellationToken cancellationToken)
    {
        var tasks = await FilterTasksAsync(
            authorization,
            await application.ListTasksByStatusAsync(status, DefaultTaskLimit, cancellationToken),
            cancellationToken);
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

    private async Task<TelegramResponse> ParallelTasksSettingsMenuAsync(CancellationToken cancellationToken)
    {
        var settings = await application.GetRunnerSettingsAsync(cancellationToken);

        return RenderParallelTasksSettings(settings);
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

    private async Task<TelegramResponse> SetAgentMaxParallelTasksAsync(
        int maxParallelTasks,
        CancellationToken cancellationToken)
    {
        var result = await application.SetAgentMaxParallelTasksAsync(maxParallelTasks, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        return RenderParallelTasksSettings(result.Value!, await RestartAgentNoticeAsync(cancellationToken));
    }

    private async Task<TelegramResponse> SetPrivateChatThreadsAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        var result = await application.SetPrivateChatThreadsAsync(enabled, cancellationToken);

        if (!result.IsSuccess)
        {
            return new TelegramResponse($"{result.Error!.Code}: {result.Error.Message}", BackButtons());
        }

        return RenderSettings(result.Value!, "Telegram private thread routing updated.");
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
        CancellationToken cancellationToken,
        bool includeTerminalNavigation = false)
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
        var taskMetadata = string.Join(
            '\n',
            [
                $"Task: {task.Id}",
                $"Title: {task.Title}",
                $"Project: {task.ProjectId}",
                $"Machine: {task.MachineId}",
                $"Status: {task.Status.ToStorageValue()}",
                $"Iterations: {task.CurrentIteration}/{task.MaxIterations}",
                $"Latest iteration: {FormatIteration(latestIteration)}",
                $"Runner: {FormatRunnerExecution(latestExecution)}",
            ]);
        var goalPreview = Truncate(task.Goal, TaskGoalPreviewLength);
        var followUpPreview = string.IsNullOrWhiteSpace(snapshot.LatestFollowUp)
            ? null
            : Truncate(snapshot.LatestFollowUp, TaskFollowUpPreviewLength);
        var followUpBlock = followUpPreview is null
            ? string.Empty
            : $"""

            Latest follow-up:
            {TelegramMarkdown.Quote(followUpPreview)}
            """;

        return new TelegramResponse(
            $"""
            Task details:
            {TelegramMarkdown.Quote(taskMetadata)}

            Goal:
            {TelegramMarkdown.Quote(goalPreview)}
            {followUpBlock}
            """,
            TaskDetailButtons(task, includeTerminalNavigation),
            new TelegramResponseMetadata(TelegramResponseKind.TaskDetails, task.Id));
    }

    private async Task<TelegramResponse> ViewTaskAsync(
        TelegramUserAuthorization authorization,
        TaskId taskId,
        int? messageThreadId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", TaskMenuButtons());
        }

        if (!await CanAccessProjectAsync(authorization, task.Value!.ProjectId, cancellationToken))
        {
            return new TelegramResponse("You do not have access to this task.", TaskMenuButtons());
        }

        return await RenderTaskDetailsAsync(
            taskId,
            cancellationToken,
            includeTerminalNavigation: messageThreadId is null);
    }

    private async Task<TelegramResponse> ViewTaskResultAsync(
        TelegramUserAuthorization authorization,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetAccessibleTaskReviewAsync(authorization, taskId, cancellationToken);

        if (!snapshot.IsSuccess)
        {
            return new TelegramResponse($"{snapshot.Error!.Code}: {snapshot.Error.Message}", TaskMenuButtons());
        }

        var response = snapshot.Value!.LatestRunnerResponse;
        var text = string.IsNullOrWhiteSpace(response)
            ? "(none yet)"
            : Truncate(response, TaskResultPreviewLength);

        return new TelegramResponse(
            $"Task result: {taskId}\n{TelegramMarkdown.Quote(text)}",
            TaskSubviewButtons(taskId),
            new TelegramResponseMetadata(TelegramResponseKind.TaskDetails, taskId));
    }

    private async Task<TelegramResponse> ViewTaskProgressAsync(
        TelegramUserAuthorization authorization,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetAccessibleTaskReviewAsync(authorization, taskId, cancellationToken);

        if (!snapshot.IsSuccess)
        {
            return new TelegramResponse($"{snapshot.Error!.Code}: {snapshot.Error.Message}", TaskMenuButtons());
        }

        var events = snapshot.Value!.RuntimeEvents
            .OrderByDescending(runtimeEvent => runtimeEvent.CreatedAt)
            .Take(TaskProgressLimit)
            .OrderBy(runtimeEvent => runtimeEvent.CreatedAt)
            .Select(runtimeEvent =>
                $"- {runtimeEvent.CreatedAt:HH:mm:ss} {runtimeEvent.EventType}: {Truncate(runtimeEvent.Message, TaskProgressMessageLength)}")
            .ToArray();
        var text = events.Length == 0 ? "(none yet)" : string.Join('\n', events);

        return new TelegramResponse(
            $"Task progress: {taskId}\n{TelegramMarkdown.Quote(text)}",
            TaskSubviewButtons(taskId),
            new TelegramResponseMetadata(TelegramResponseKind.TaskDetails, taskId));
    }

    private async Task<TelegramResponse> ViewTaskArtifactsAsync(
        TelegramUserAuthorization authorization,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetAccessibleTaskReviewAsync(authorization, taskId, cancellationToken);

        if (!snapshot.IsSuccess)
        {
            return new TelegramResponse($"{snapshot.Error!.Code}: {snapshot.Error.Message}", TaskMenuButtons());
        }

        var artifacts = snapshot.Value!.Artifacts
            .OrderByDescending(artifact => artifact.CreatedAt)
            .Take(TaskArtifactsLimit)
            .OrderBy(artifact => artifact.CreatedAt)
            .Select(artifact =>
                $"- {artifact.Type.ToStorageValue()}: {Truncate(FormatArtifactPath(snapshot.Value.ArtifactRootPath, artifact.RelativePath), TaskArtifactPathLength)}")
            .ToArray();
        var text = artifacts.Length == 0 ? "(none)" : string.Join('\n', artifacts);

        return new TelegramResponse(
            $"Task artifacts: {taskId}\n{TelegramMarkdown.Quote(text)}",
            TaskSubviewButtons(taskId),
            new TelegramResponseMetadata(TelegramResponseKind.TaskDetails, taskId));
    }

    private async Task<ApplicationResult<TelegramTaskReviewSnapshot>> GetAccessibleTaskReviewAsync(
        TelegramUserAuthorization authorization,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return ApplicationResult<TelegramTaskReviewSnapshot>.Failure(task.Error!.Code, task.Error.Message);
        }

        if (!await CanAccessProjectAsync(authorization, task.Value!.ProjectId, cancellationToken))
        {
            return ApplicationResult<TelegramTaskReviewSnapshot>.Failure(
                "task_access_denied",
                "You do not have access to this task.");
        }

        return await application.GetTaskReviewAsync(taskId, cancellationToken);
    }

    public async Task<RuntimeTask?> GetTaskOrDefaultAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        return task.IsSuccess ? task.Value : null;
    }

    /// <summary>
    /// Lists recent runtime events for a task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="limit">The maximum number of most recent events to return.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The task runtime events in chronological order.</returns>
    public async Task<IReadOnlyList<RuntimeEvent>> ListTaskRuntimeEventsAsync(
        TaskId taskId,
        int limit,
        CancellationToken cancellationToken) =>
        await application.ListTaskRuntimeEventsAsync(taskId, limit, cancellationToken);

    private async Task<TelegramResponse> CancelTaskAsync(
        TelegramUserAuthorization authorization,
        TaskId taskId,
        long chatId,
        int? messageThreadId,
        CancellationToken cancellationToken)
    {
        var existing = await application.GetTaskAsync(taskId, cancellationToken);

        if (!existing.IsSuccess)
        {
            return new TelegramResponse($"{existing.Error!.Code}: {existing.Error.Message}", BackButtons());
        }

        if (!await CanAccessProjectAsync(authorization, existing.Value!.ProjectId, cancellationToken))
        {
            return new TelegramResponse("You do not have access to this task.", TaskMenuButtons());
        }

        var task = await application.CancelTaskAsync(taskId, $"telegram:{chatId}", cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        if (messageThreadId is null)
        {
            var menu = await MainMenuAsync(
                authorization,
                cancellationToken,
                $"Task cancelled: {task.Value!.Id}");

            return menu with
            {
                Metadata = new TelegramResponseMetadata(TelegramResponseKind.TaskWatch, task.Value.Id),
            };
        }

        return await RenderTaskDetailsAsync(task.Value!.Id, cancellationToken);
    }

    private async Task<TelegramResponse> CompleteTaskAsync(
        TelegramUserAuthorization authorization,
        TaskId taskId,
        int? messageThreadId,
        CancellationToken cancellationToken)
    {
        var existing = await application.GetTaskAsync(taskId, cancellationToken);

        if (!existing.IsSuccess)
        {
            return new TelegramResponse($"{existing.Error!.Code}: {existing.Error.Message}", BackButtons());
        }

        if (!await CanAccessProjectAsync(authorization, existing.Value!.ProjectId, cancellationToken))
        {
            return new TelegramResponse("You do not have access to this task.", TaskMenuButtons());
        }

        var task = await application.CompleteTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        if (messageThreadId is null)
        {
            var menu = await MainMenuAsync(
                authorization,
                cancellationToken,
                $"Task completed: {task.Value!.Id}");

            return menu with
            {
                Metadata = new TelegramResponseMetadata(TelegramResponseKind.TaskWatch, task.Value.Id),
            };
        }

        return await RenderTaskDetailsAsync(task.Value!.Id, cancellationToken);
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
            TelegramButtonMarkup.Empty);

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
        var privateThreadsTarget = !settings.PrivateChatThreadsEnabled;
        var noticeText = string.IsNullOrWhiteSpace(notice) ? "" : $"\n\n{notice}";

        return new TelegramResponse(
            $"""
            Settings

            Agent parallel tasks: {settings.AgentMaxParallelTasks}
            Codex sandbox: {settings.CodexSandboxMode}
            Codex bypass approvals and sandbox: {(settings.CodexBypassApprovalsAndSandbox ? "allowed" : "disallowed")}
            Telegram private threads: {(settings.PrivateChatThreadsEnabled ? "enabled" : "disabled")}
            {noticeText}
            """,
            Buttons(
                Row(Button(
                    $"Parallel tasks: {settings.AgentMaxParallelTasks}",
                    TelegramCallbackData.ParallelTasksSettingsMenu,
                    TelegramButtonStyle.Primary)),
                Row(Button(
                    sandboxEnabled ? "Sandbox enabled" : "Sandbox disabled",
                    TelegramCallbackData.SetCodexSandboxMode(sandboxTarget),
                    sandboxEnabled ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger),
                    Button(
                    settings.CodexBypassApprovalsAndSandbox ? "Bypass enabled" : "Bypass disabled",
                    TelegramCallbackData.SetCodexBypassApprovalsAndSandbox(bypassTarget),
                    settings.CodexBypassApprovalsAndSandbox ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger)),
                Row(Button(
                    settings.PrivateChatThreadsEnabled ? "Private threads enabled" : "Private threads disabled",
                    TelegramCallbackData.SetPrivateChatThreads(privateThreadsTarget),
                    settings.PrivateChatThreadsEnabled ? TelegramButtonStyle.Success : TelegramButtonStyle.Danger)),
                Row(Button("Back", TelegramCallbackData.MainMenu))));
    }

    private static TelegramResponse RenderParallelTasksSettings(
        TelegramRunnerSettings settings,
        string? notice = null)
    {
        var noticeText = string.IsNullOrWhiteSpace(notice) ? "" : $"\n\n{notice}";

        return new TelegramResponse(
            $"""
            Parallel tasks

            Current: {settings.AgentMaxParallelTasks}
            {noticeText}
            """,
            Buttons(
                Row(
                    ParallelTaskButton(settings, 1),
                    ParallelTaskButton(settings, 2)),
                Row(
                    ParallelTaskButton(settings, 4),
                    ParallelTaskButton(settings, 8)),
                Row(
                    ParallelTaskButton(settings, 16),
                    ParallelTaskButton(settings, 32)),
                Row(Button("Back", TelegramCallbackData.SettingsMenu))));
    }

    private static TelegramButton ParallelTaskButton(TelegramRunnerSettings settings, int value) =>
        Button(
            value.ToString(CultureInfo.InvariantCulture),
            TelegramCallbackData.SetAgentMaxParallelTasks(value),
            settings.AgentMaxParallelTasks == value ? TelegramButtonStyle.Success : TelegramButtonStyle.Primary);

    private static TelegramButtonMarkup TaskDetailButtons(
        RuntimeTask task,
        bool includeTerminalNavigation)
    {
        var submenuRow = Row(
            Button("Result", TelegramCallbackData.ViewTaskResult(task.Id)),
            Button("Progress", TelegramCallbackData.ViewTaskProgress(task.Id)),
            Button("Artifacts", TelegramCallbackData.ViewTaskArtifacts(task.Id)));

        if (task.Status == RuntimeTaskStatus.Reviewing)
        {
            return Buttons(
                submenuRow,
                Row(
                    Button("Continue", TelegramCallbackData.ContinueTask(task.Id), TelegramButtonStyle.Primary),
                    Button("Complete", TelegramCallbackData.CompleteTask(task.Id))),
                Row(
                    Button("Cancel", TelegramCallbackData.CancelTask(task.Id)),
                    Button("Back", TelegramCallbackData.MainMenu)));
        }

        if (task.Status == RuntimeTaskStatus.Cancelled)
        {
            return includeTerminalNavigation
                ? Buttons(
                    submenuRow,
                    Row(
                        Button("Continue", TelegramCallbackData.ContinueTask(task.Id), TelegramButtonStyle.Primary),
                        Button("Menu", TelegramCallbackData.MainMenu)))
                : Buttons(
                    submenuRow,
                    Row(Button("Continue", TelegramCallbackData.ContinueTask(task.Id), TelegramButtonStyle.Primary)));
        }

        if (task.Status.IsTerminal())
        {
            return includeTerminalNavigation
                ? Buttons(
                    submenuRow,
                    Row(Button("Menu", TelegramCallbackData.MainMenu)))
                : Buttons(submenuRow);
        }

        return Buttons(
            submenuRow,
            Row(
                Button("Cancel", TelegramCallbackData.CancelTask(task.Id)),
                Button("Back", TelegramCallbackData.MainMenu)));
    }

    private static bool CanContinueTask(RuntimeTaskStatus status) =>
        status is RuntimeTaskStatus.Reviewing or RuntimeTaskStatus.Cancelled;

    private static TelegramButtonMarkup TaskSubviewButtons(TaskId taskId) =>
        Buttons(Row(Button("Back", TelegramCallbackData.ViewTask(taskId))));

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

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();

        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..Math.Max(0, maxLength - 14)].TrimEnd() + "\n[truncated]";
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

    private async Task<IReadOnlyList<RuntimeProject>> ListAccessibleProjectsAsync(
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var projects = await application.ListProjectsAsync(cancellationToken);

        return authorization.IsAdmin
            ? projects
            : await FilterProjectsAsync(authorization, projects, cancellationToken);
    }

    private async Task<IReadOnlyList<RuntimeProject>> ListAccessibleActiveProjectsAsync(
        TelegramUserAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var projects = await application.ListActiveProjectsAsync(cancellationToken);

        return authorization.IsAdmin
            ? projects
            : await FilterProjectsAsync(authorization, projects, cancellationToken);
    }

    private async Task<IReadOnlyList<RuntimeProject>> FilterProjectsAsync(
        TelegramUserAuthorization authorization,
        IReadOnlyList<RuntimeProject> projects,
        CancellationToken cancellationToken)
    {
        var access = await application.GetTelegramUserAccessAsync(authorization.User.Id, cancellationToken);

        if (!access.IsSuccess)
        {
            return [];
        }

        var projectIds = access.Value!.ProjectAccess.Select(project => project.ProjectId).ToHashSet();
        var groupIds = access.Value.ProjectGroupAccess.Select(group => group.ProjectGroupId).ToHashSet();

        return [.. projects.Where(project =>
            projectIds.Contains(project.Id)
            || (project.GroupId is not null && groupIds.Contains(project.GroupId.Value)))];
    }

    private async Task<IReadOnlyList<RuntimeTask>> FilterTasksAsync(
        TelegramUserAuthorization authorization,
        IReadOnlyList<RuntimeTask> tasks,
        CancellationToken cancellationToken)
    {
        if (authorization.IsAdmin)
        {
            return tasks;
        }

        var projects = await ListAccessibleProjectsAsync(authorization, cancellationToken);
        var projectIds = projects.Select(project => project.Id).ToHashSet();

        return [.. tasks.Where(task => projectIds.Contains(task.ProjectId))];
    }

    private async Task<bool> CanAccessProjectAsync(
        TelegramUserAuthorization authorization,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        if (authorization.IsAdmin)
        {
            return true;
        }

        var project = await application.GetProjectAsync(projectId, cancellationToken);

        return project.IsSuccess && await CanAccessProjectAsync(authorization, project.Value!, cancellationToken);
    }

    private async Task<bool> CanAccessProjectAsync(
        TelegramUserAuthorization authorization,
        RuntimeProject project,
        CancellationToken cancellationToken)
    {
        if (authorization.IsAdmin)
        {
            return true;
        }

        var access = await application.GetTelegramUserAccessAsync(authorization.User.Id, cancellationToken);

        if (!access.IsSuccess)
        {
            return false;
        }

        var hasProjectAccess = access.Value!.ProjectAccess.Any(grant => grant.ProjectId == project.Id);
        var hasGroupAccess = project.GroupId is not null
            && access.Value.ProjectGroupAccess.Any(grant => grant.ProjectGroupId == project.GroupId.Value);

        return hasProjectAccess || hasGroupAccess;
    }

    private static string FormatTelegramUserStatus(RuntimeTelegramUser user) =>
        $"{user.Role.ToStorageValue()}/{user.Status.ToStorageValue()}";

    private static string FormatTelegramUserDisplay(RuntimeTelegramUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return $"@{user.Username}";
        }

        var name = string.Join(
            ' ',
            new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(name) ? user.ChatId.ToString(CultureInfo.InvariantCulture) : name;
    }

    private static string FormatTelegramUserMetadata(
        RuntimeTelegramUser user,
        bool includeGrants,
        int projectGrantCount,
        int groupGrantCount)
    {
        var lines = new List<string>
        {
            $"User: {user.Id}",
            $"Chat: {user.ChatId}",
            $"Username: {FormatOptional(user.Username is null ? null : $"@{user.Username}")}",
            $"First name: {FormatOptional(user.FirstName)}",
            $"Last name: {FormatOptional(user.LastName)}",
            $"Role: {user.Role.ToStorageValue()}",
            $"Status: {user.Status.ToStorageValue()}",
        };

        if (includeGrants)
        {
            lines.Add($"Project grants: {projectGrantCount}");
            lines.Add($"Group grants: {groupGrantCount}");
        }

        return string.Join('\n', lines);
    }

    private static string FormatOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value;

    private static long GetSenderScope(TelegramUpdate update) =>
        update.SenderUserId ?? update.ChatId;

    private TelegramConversationKey GetConversationKey(TelegramUpdate update) =>
        new(update.ChatId, GetRoutingThreadId(update));

    /// <summary>
    /// Gets the message thread id that should be used for Aeges routing.
    /// </summary>
    /// <param name="update">The Telegram update.</param>
    /// <returns>The routed message thread id, or <see langword="null"/> when the update belongs to the chat root.</returns>
    public int? GetRoutingThreadId(TelegramUpdate update) =>
        update.IsPrivateChat && !configuration.EnablePrivateChatThreads
            ? null
            : update.MessageThreadId;

    private string CreateTalkSource(TelegramUpdate update)
    {
        var threadId = GetRoutingThreadId(update);

        return threadId is null
            ? $"telegram:{update.ChatId}"
            : $"telegram:{update.ChatId}:thread:{threadId.Value}";
    }

    private static TelegramButton Button(
        string text,
        string callbackData,
        TelegramButtonStyle style = TelegramButtonStyle.Default) =>
        new(text, callbackData, style);

    private static IReadOnlyList<TelegramButton> Row(params TelegramButton[] buttons) =>
        buttons;

    private static IReadOnlyList<TelegramButton>[] Grid(IEnumerable<TelegramButton> buttons) =>
        buttons.Chunk(ButtonGridColumns).Select(static chunk => Row(chunk)).ToArray();

    private static IReadOnlyList<TelegramButton>[] ProjectGrid(IEnumerable<TelegramButton> buttons)
    {
        var rows = new List<IReadOnlyList<TelegramButton>>();
        var compactRow = new List<TelegramButton>(ButtonGridColumns);

        foreach (var button in buttons)
        {
            if (button.Text.Length > WideProjectButtonTextLength)
            {
                FlushCompactRow();
                rows.Add(Row(button));

                continue;
            }

            compactRow.Add(button);

            if (compactRow.Count == ButtonGridColumns)
            {
                FlushCompactRow();
            }
        }

        FlushCompactRow();

        return [.. rows];

        void FlushCompactRow()
        {
            if (compactRow.Count == 0)
            {
                return;
            }

            rows.Add(Row([.. compactRow]));
            compactRow.Clear();
        }
    }

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

    private readonly record struct TelegramConversationKey(
        long ChatId,
        int? MessageThreadId);

    private sealed record TaskContinuationDraft(
        TaskId TaskId,
        long SenderUserId,
        int? MessageThreadId,
        int? PromptMessageId,
        bool RequiresReplyToPrompt);

    private enum TaskDraftStep
    {
        ChoosingMachine,
        AwaitingTitle,
        AwaitingGoal,
    }
}
