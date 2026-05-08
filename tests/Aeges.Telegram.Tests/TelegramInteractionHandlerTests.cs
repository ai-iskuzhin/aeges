using Aeges.Application;
using Aeges.Application.Configuration;
using Aeges.Core;
using Aeges.Telegram;

namespace Aeges.Telegram.Tests;

public sealed class TelegramInteractionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_returns_button_menu_for_plain_text_messages()
    {
        var handler = new TelegramInteractionHandler(new FakeTelegramApplicationFacade(), new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(new TelegramUpdate(1001, Text: "hello"), CancellationToken.None);

        Assert.Equal("Aeges control", response.Text);
        Assert.Equal(2, response.Buttons.Rows.Count);
        Assert.Equal("Projects", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ListProjects, response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Machines", response.Buttons.Rows[0][1].Text);
        Assert.Equal(TelegramCallbackData.ListMachines, response.Buttons.Rows[0][1].CallbackData);
        Assert.Equal("Queued tasks", response.Buttons.Rows[1][0].Text);
        Assert.Equal(TelegramCallbackData.ListQueuedTasks, response.Buttons.Rows[1][0].CallbackData);
    }

    [Fact]
    public async Task HandleAsync_rejects_unauthorized_chats_without_calling_application()
    {
        var facade = new FakeTelegramApplicationFacade();
        var handler = new TelegramInteractionHandler(
            facade,
            new AegesTelegramConfiguration { AllowedChatIds = [1001] });

        var response = await handler.HandleAsync(
            new TelegramUpdate(2002, CallbackData: TelegramCallbackData.ListProjects),
            CancellationToken.None);

        Assert.Equal("This Telegram chat is not authorized for Aeges.", response.Text);
        Assert.Empty(response.Buttons.Rows);
        Assert.Equal(0, facade.ListProjectsCallCount);
    }

    [Fact]
    public async Task HandleAsync_lists_projects_from_button_callback()
    {
        var facade = new FakeTelegramApplicationFacade
        {
            Projects =
            [
                RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now),
            ],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ListProjects),
            CancellationToken.None);

        Assert.Equal("Projects:\n- project-aeges: Aeges", response.Text);
        AssertBackButton(response);
    }

    [Fact]
    public async Task HandleAsync_lists_machines_from_button_callback()
    {
        var machine = RuntimeMachine.Create(new MachineId("machine-local"), "Local", "macOS", Now);
        machine.MarkOnline(Now);

        var facade = new FakeTelegramApplicationFacade { Machines = [machine] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ListMachines),
            CancellationToken.None);

        Assert.Equal("Machines:\n- machine-local: Local (online)", response.Text);
        AssertBackButton(response);
    }

    [Fact]
    public async Task HandleAsync_lists_queued_tasks_with_task_detail_buttons()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire Telegram buttons",
            "Expose Telegram actions through inline buttons.",
            Now);

        var facade = new FakeTelegramApplicationFacade { QueuedTasks = [task] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ListQueuedTasks),
            CancellationToken.None);

        Assert.Equal("Queued tasks:\n- task-001: Wire Telegram buttons", response.Text);
        Assert.Equal("Wire Telegram buttons", response.Buttons.Rows[0][0].Text);
        Assert.Equal("aeges:task:task-001", response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[1][0].Text);
    }

    [Fact]
    public async Task HandleAsync_shows_task_details_from_button_callback()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire Telegram buttons",
            "Expose Telegram actions through inline buttons.",
            Now);

        var facade = new FakeTelegramApplicationFacade { Task = task };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewTask(task.Id)),
            CancellationToken.None);

        Assert.Contains("Task: task-001", response.Text, StringComparison.Ordinal);
        Assert.Contains("Title: Wire Telegram buttons", response.Text, StringComparison.Ordinal);
        Assert.Contains("Status: queued", response.Text, StringComparison.Ordinal);
        Assert.Contains("Expose Telegram actions through inline buttons.", response.Text, StringComparison.Ordinal);
        AssertBackButton(response);
    }

    [Fact]
    public async Task HandleAsync_returns_menu_for_unknown_or_malformed_callbacks()
    {
        var handler = new TelegramInteractionHandler(new FakeTelegramApplicationFacade(), new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: "aeges:task:   "),
            CancellationToken.None);

        Assert.Equal("Unknown action. Choose an Aeges action below.", response.Text);
        Assert.Equal(TelegramCallbackData.ListProjects, response.Buttons.Rows[0][0].CallbackData);
    }

    private static void AssertBackButton(TelegramResponse response)
    {
        Assert.Single(response.Buttons.Rows);
        Assert.Single(response.Buttons.Rows[0]);
        Assert.Equal("Back", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.MainMenu, response.Buttons.Rows[0][0].CallbackData);
    }

    private sealed class FakeTelegramApplicationFacade : ITelegramApplicationFacade
    {
        public IReadOnlyList<RuntimeProject> Projects { get; init; } = [];

        public IReadOnlyList<RuntimeMachine> Machines { get; init; } = [];

        public IReadOnlyList<RuntimeTask> QueuedTasks { get; init; } = [];

        public RuntimeTask? Task { get; init; }

        public int ListProjectsCallCount { get; private set; }

        public Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken)
        {
            ListProjectsCallCount++;
            return System.Threading.Tasks.Task.FromResult(Projects);
        }

        public Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(Machines);

        public Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(
            int limit,
            CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(QueuedTasks.Take(limit).ToArray() as IReadOnlyList<RuntimeTask>);

        public Task<ApplicationResult<RuntimeTask>> GetTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            var result = Task is not null && Task.Id == taskId
                ? ApplicationResult<RuntimeTask>.Success(Task)
                : ApplicationResult<RuntimeTask>.Failure("task_not_found", $"Task '{taskId}' was not found.");

            return System.Threading.Tasks.Task.FromResult(result);
        }
    }
}
