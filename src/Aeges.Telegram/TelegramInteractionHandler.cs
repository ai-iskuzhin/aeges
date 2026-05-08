using Aeges.Application.Configuration;
using Aeges.Core;

namespace Aeges.Telegram;

/// <summary>
/// Handles Telegram interactions using button callback payloads.
/// </summary>
public sealed class TelegramInteractionHandler
{
    private const int DefaultTaskLimit = 10;
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
            _ when TelegramCallbackData.TryParseViewTask(callbackData, out var taskId) =>
                await ViewTaskAsync(taskId, cancellationToken),
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
                Row(Button("Queued tasks", TelegramCallbackData.ListQueuedTasks))));

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

    private async Task<TelegramResponse> ViewTaskAsync(
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var task = await application.GetTaskAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            return new TelegramResponse($"{task.Error!.Code}: {task.Error.Message}", BackButtons());
        }

        return new TelegramResponse(
            $"""
            Task: {task.Value!.Id}
            Title: {task.Value.Title}
            Status: {task.Value.Status.ToStorageValue()}
            Iterations: {task.Value.CurrentIteration}/{task.Value.MaxIterations}

            Goal:
            {task.Value.Goal}
            """,
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
