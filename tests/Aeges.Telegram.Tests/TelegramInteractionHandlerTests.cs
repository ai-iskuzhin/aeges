using Aeges.Application;
using Aeges.Application.Configuration;
using Aeges.Application.TelegramUsers;
using Aeges.Core;
using Aeges.Telegram;

namespace Aeges.Telegram.Tests;

public sealed class TelegramInteractionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_sends_plain_text_to_talk_session()
    {
        var facade = new FakeTelegramApplicationFacade();
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(new TelegramUpdate(1001, Text: "hello"), CancellationToken.None);

        Assert.StartsWith("> Session: talk-001\n> Runner: codex", response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Talk:", response.Text, StringComparison.Ordinal);
        Assert.Contains("Talk response to: hello", response.Text, StringComparison.Ordinal);
        Assert.Empty(response.Buttons.Rows);
        Assert.Equal("hello", facade.TalkMessage);
    }

    [Fact]
    public async Task HandleAsync_shows_main_menu_for_start_command()
    {
        var facade = new FakeTelegramApplicationFacade();
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(new TelegramUpdate(1001, Text: "/start"), CancellationToken.None);

        Assert.Equal("Aeges control", response.Text);
        Assert.Equal("New task", response.Buttons.Rows[0][0].Text);
        Assert.Null(facade.TalkMessage);
    }

    [Fact]
    public async Task HandleAsync_shows_runtime_version_when_configured()
    {
        var facade = new FakeTelegramApplicationFacade();
        var handler = new TelegramInteractionHandler(
            facade,
            new AegesTelegramConfiguration(),
            runtimeVersion: "0.1.0-test");

        var response = await handler.HandleAsync(new TelegramUpdate(1001, Text: "/start"), CancellationToken.None);

        Assert.Equal("Aeges control\nVersion: 0.1.0-test", response.Text);
    }

    [Fact]
    public async Task HandleAsync_explains_pending_text_cancellation_limit()
    {
        var handler = new TelegramInteractionHandler(
            new FakeTelegramApplicationFacade(),
            new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.CancelPendingTextResponse),
            CancellationToken.None);

        Assert.Contains("already finished", response.Text, StringComparison.Ordinal);
        Assert.Empty(response.Buttons.Rows);
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
    public async Task HandleAsync_blocks_pending_telegram_user()
    {
        var pending = RuntimeTelegramUser.CreatePending(2002, Now);
        var facade = new FakeTelegramApplicationFacade { TelegramUsers = [pending] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(2002, Text: "hello", Username: "new-user", FirstName: "New", LastName: "User"),
            CancellationToken.None);

        Assert.Contains("waiting for an Aeges Telegram administrator", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Username: @new-user", response.Text, StringComparison.Ordinal);
        Assert.Contains("> First name: New", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Last name: User", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Status: pending", response.Text, StringComparison.Ordinal);
        Assert.Empty(response.Buttons.Rows);
        Assert.Null(facade.TalkMessage);
    }

    [Fact]
    public async Task HandleAsync_lists_telegram_users_for_admins()
    {
        var pending = RuntimeTelegramUser.CreatePending(
            2002,
            Now,
            new RuntimeTelegramUserProfile("guest", "Guest", "Operator"));
        var facade = new FakeTelegramApplicationFacade
        {
            TelegramUsers =
            [
                RuntimeTelegramUser.CreateFirstAdmin(1001, Now),
                pending,
            ],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.UserMenu),
            CancellationToken.None);

        Assert.Contains("@guest", response.Text, StringComparison.Ordinal);
        Assert.Contains("user / pending", response.Text, StringComparison.Ordinal);
        Assert.Equal(TelegramButtonStyle.Danger, response.Buttons.Rows[0][1].Style);
    }

    [Fact]
    public async Task HandleAsync_does_not_allow_admin_to_deny_self()
    {
        var admin = RuntimeTelegramUser.CreateFirstAdmin(1001, Now);
        var facade = new FakeTelegramApplicationFacade { TelegramUsers = [admin] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var details = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewTelegramUser(admin.Id)),
            CancellationToken.None);
        var denied = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.DenyTelegramUser(admin.Id)),
            CancellationToken.None);

        Assert.DoesNotContain(
            details.Buttons.Rows.SelectMany(row => row),
            button => button.Text == "Deny");
        Assert.Contains("You cannot deny your own Telegram user.", denied.Text, StringComparison.Ordinal);
        Assert.Equal(TelegramUserStatus.Approved, admin.Status);
    }

    [Fact]
    public async Task HandleAsync_renders_project_access_as_red_until_granted()
    {
        var user = RuntimeTelegramUser.CreatePending(2002, Now);
        user.Approve(Now);
        var projectId = new ProjectId("project-aeges");
        var facade = new FakeTelegramApplicationFacade
        {
            TelegramUsers =
            [
                RuntimeTelegramUser.CreateFirstAdmin(1001, Now),
                user,
            ],
            Projects =
            [
                RuntimeProject.Create(projectId, "Aeges", "/workspace/aeges", Now),
            ],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var blocked = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewTelegramUser(user.Id)),
            CancellationToken.None);
        var granted = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.SetTelegramProjectAccess(user.Id, projectId, allowed: true)),
            CancellationToken.None);

        var blockedProjectButton = blocked.Buttons.Rows.SelectMany(row => row).Single(button => button.Text == "Aeges");
        var grantedProjectButton = granted.Buttons.Rows.SelectMany(row => row).Single(button => button.Text == "Aeges");

        Assert.Contains("> Project grants: 0", blocked.Text, StringComparison.Ordinal);
        Assert.Equal(TelegramButtonStyle.Danger, blockedProjectButton.Style);
        Assert.Contains("> Project grants: 1", granted.Text, StringComparison.Ordinal);
        Assert.Equal(TelegramButtonStyle.Success, grantedProjectButton.Style);
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

        Assert.Equal("Projects", response.Text);
        Assert.Equal("Ungrouped (1)", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ViewUngroupedProjects, response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[1][0].Text);
        Assert.Equal(TelegramCallbackData.MainMenu, response.Buttons.Rows[1][0].CallbackData);
    }

    [Fact]
    public async Task HandleAsync_lists_projects_grouped_by_project_group()
    {
        var facade = new FakeTelegramApplicationFacade
        {
            ProjectGroups =
            [
                RuntimeProjectGroup.Create(new ProjectGroupId("analitex"), "Analitex", Now, "/workspace/analitex"),
            ],
            Projects =
            [
                RuntimeProject.Create(
                    new ProjectId("analitex-api"),
                    "analitex-api",
                    "/workspace/analitex/api",
                    Now,
                    new ProjectGroupId("analitex")),
                RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now),
            ],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ListProjects),
            CancellationToken.None);

        Assert.Equal("Projects", response.Text);
        Assert.Equal("Analitex (1)", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ViewProjectGroup(new ProjectGroupId("analitex")), response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Ungrouped (1)", response.Buttons.Rows[0][1].Text);

        var groupResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewProjectGroup(new ProjectGroupId("analitex"))),
            CancellationToken.None);

        Assert.Equal("Analitex projects", groupResponse.Text);
        Assert.Equal("analitex-api", groupResponse.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ViewProject(new ProjectId("analitex-api")), groupResponse.Buttons.Rows[0][0].CallbackData);

        var ungroupedResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewUngroupedProjects),
            CancellationToken.None);

        Assert.Equal("Ungrouped projects", ungroupedResponse.Text);
        Assert.Equal("Aeges", ungroupedResponse.Buttons.Rows[0][0].Text);
    }

    [Fact]
    public async Task HandleAsync_shows_project_details_with_task_status_buttons()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Queued work",
            "Do the work.",
            Now);
        var facade = new FakeTelegramApplicationFacade
        {
            Projects = [RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now)],
            QueuedTasks = [task],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewProject(new ProjectId("project-aeges"))),
            CancellationToken.None);

        Assert.Contains("Project details:\n> Project: project-aeges", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Name: Aeges", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Status: active", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Path: /workspace/aeges", response.Text, StringComparison.Ordinal);
        Assert.Equal("queued (1)", response.Buttons.Rows[0][0].Text);
        Assert.Equal(
            TelegramCallbackData.ListProjectTasksByStatus(new ProjectId("project-aeges"), RuntimeTaskStatus.Queued),
            response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("planning (0)", response.Buttons.Rows[0][1].Text);
        Assert.Equal("New task", response.Buttons.Rows[^2][0].Text);
        Assert.Equal(TelegramCallbackData.SelectTaskProject(new ProjectId("project-aeges")), response.Buttons.Rows[^2][0].CallbackData);
        Assert.Equal("Archive", response.Buttons.Rows[^2][1].Text);
        Assert.Equal(TelegramCallbackData.ArchiveProject(new ProjectId("project-aeges")), response.Buttons.Rows[^2][1].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[^1][0].Text);
        Assert.Equal(TelegramCallbackData.ListProjects, response.Buttons.Rows[^1][0].CallbackData);
    }

    [Fact]
    public async Task HandleAsync_asks_for_confirmation_before_archiving_project()
    {
        var project = RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now);
        var facade = new FakeTelegramApplicationFacade
        {
            Projects = [project],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ArchiveProject(project.Id)),
            CancellationToken.None);

        Assert.Null(facade.ArchivedProjectId);
        Assert.False(project.IsArchived);
        Assert.Contains("Archive this project?", response.Text, StringComparison.Ordinal);
        Assert.Equal("Confirm archive", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ConfirmArchiveProject(project.Id), response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[0][1].Text);
        Assert.Equal(TelegramCallbackData.ViewProject(project.Id), response.Buttons.Rows[0][1].CallbackData);
    }

    [Fact]
    public async Task HandleAsync_archives_project_after_confirmation()
    {
        var project = RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now);
        var facade = new FakeTelegramApplicationFacade
        {
            Projects = [project],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ConfirmArchiveProject(project.Id)),
            CancellationToken.None);

        Assert.Equal(project.Id, facade.ArchivedProjectId);
        Assert.True(project.IsArchived);
        Assert.Contains("Project archived.", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Status: archived", response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            response.Buttons.Rows.SelectMany(row => row),
            button => button.CallbackData == TelegramCallbackData.ArchiveProject(project.Id));
    }

    [Fact]
    public async Task HandleAsync_lists_project_tasks_by_status()
    {
        var matchingTask = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Aeges queued work",
            "Do the Aeges work.",
            Now);
        var otherProjectTask = RuntimeTask.Create(
            new TaskId("task-002"),
            new ProjectId("project-other"),
            new MachineId("machine-local"),
            "Other queued work",
            "Do the other work.",
            Now);
        var facade = new FakeTelegramApplicationFacade
        {
            Projects = [RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now)],
            QueuedTasks = [matchingTask, otherProjectTask],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(
                1001,
                CallbackData: TelegramCallbackData.ListProjectTasksByStatus(
                    new ProjectId("project-aeges"),
                    RuntimeTaskStatus.Queued)),
            CancellationToken.None);

        Assert.Equal("Aeges queued tasks:\n- task-001: Aeges queued work", response.Text);
        Assert.Equal("Aeges queued work", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ViewTask(matchingTask.Id), response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[1][0].Text);
        Assert.Equal(TelegramCallbackData.ViewProject(new ProjectId("project-aeges")), response.Buttons.Rows[1][0].CallbackData);
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

        Assert.Equal($"Machines:\n- machine-local: Local (online, last seen {Now:O})", response.Text);
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

        Assert.Equal("queued tasks:\n- task-001: Wire Telegram buttons", response.Text);
        Assert.Equal("Wire Telegram buttons", response.Buttons.Rows[0][0].Text);
        Assert.Equal("aeges:task:task-001", response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[1][0].Text);
    }

    [Fact]
    public async Task HandleAsync_shows_task_status_menu()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Queued work",
            "Do the work.",
            Now);
        var facade = new FakeTelegramApplicationFacade { QueuedTasks = [task] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.TaskMenu),
            CancellationToken.None);

        Assert.Equal("Tasks by status", response.Text);
        Assert.Equal("queued (1)", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ListTasksByStatus(RuntimeTaskStatus.Queued), response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("planning (0)", response.Buttons.Rows[0][1].Text);
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

        Assert.Contains("Task details:\n> Task: task-001", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Title: Wire Telegram buttons", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Status: queued", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Expose Telegram actions through inline buttons.", response.Text, StringComparison.Ordinal);
        Assert.Equal("Cancel", response.Buttons.Rows[0][0].Text);
        Assert.Equal("ae:t:x:task-001", response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[0][1].Text);
    }

    [Fact]
    public async Task HandleAsync_shows_review_details_and_completion_button_for_reviewing_task()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire Telegram buttons",
            "Expose Telegram actions through inline buttons.",
            Now);
        task.StartPlanning(Now);
        task.StartRunning(Now);
        task.StartReview(Now);
        var iteration = TaskIteration.Create(
            new IterationId("iteration-001"),
            task.Id,
            1,
            new RunnerId("mock"),
            Now);
        iteration.StartRunning(Now);
        iteration.StartReview(Now);
        iteration.Complete(Now);
        var artifact = new RuntimeArtifact(
            new ArtifactId("artifact-001"),
            task.Id,
            iteration.Id,
            ArtifactType.Result,
            "project-aeges/task-001/iteration-001/result.md",
            Now);
        var execution = RuntimeRunnerExecution.Start(
            new RunnerExecutionId("runner-execution-001"),
            task.Id,
            iteration.Id,
            new RunnerId("mock"),
            "runner:mock",
            "/tmp/worktree",
            Now);
        execution.RecordExit(0, Now);
        var facade = new FakeTelegramApplicationFacade
        {
            Task = task,
            ReviewSnapshot = new TelegramTaskReviewSnapshot(
                task,
                [iteration],
                [artifact],
                "/runtime/artifacts",
                [execution],
                "Mock runner result: success."),
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewTask(task.Id)),
            CancellationToken.None);

        Assert.Contains("> Status: reviewing", response.Text, StringComparison.Ordinal);
        Assert.Contains("Runner response:\n> Mock runner result: success.", response.Text, StringComparison.Ordinal);
        Assert.Contains("Artifacts:\n> - result: /runtime/artifacts/project-aeges/task-001/iteration-001/result.md", response.Text, StringComparison.Ordinal);
        Assert.Equal("Continue", response.Buttons.Rows[0][0].Text);
        Assert.Equal("ae:t:more:task-001", response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal(TelegramButtonStyle.Primary, response.Buttons.Rows[0][0].Style);
        Assert.Equal("Complete", response.Buttons.Rows[0][1].Text);
        Assert.Equal("ae:t:done:task-001", response.Buttons.Rows[0][1].CallbackData);
    }

    [Fact]
    public async Task HandleAsync_continues_reviewing_task_with_follow_up_feedback()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Fix smoke test",
            "Update notes/status.txt.",
            Now);
        task.StartPlanning(Now);
        task.AdvanceIteration(Now);
        task.StartRunning(Now);
        task.StartReview(Now);
        var facade = new FakeTelegramApplicationFacade { Task = task };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var prompt = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ContinueTask(task.Id)),
            CancellationToken.None);
        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, Text: "Please retry with sandbox disabled."),
            CancellationToken.None);

        Assert.Equal("Send the follow-up instructions for the next iteration.", prompt.Text);
        Assert.True(facade.ContinueTaskCalled);
        Assert.Equal("Please retry with sandbox disabled.", facade.ContinuedFeedback);
        Assert.Contains("Task continued: task-001", response.Text, StringComparison.Ordinal);
        Assert.Contains("Status: queued", response.Text, StringComparison.Ordinal);
        Assert.Contains("> Please retry with sandbox disabled.", response.Text, StringComparison.Ordinal);
        Assert.Contains("Agent:\n> Agent started.", response.Text, StringComparison.Ordinal);
        Assert.Equal(TelegramResponseKind.TaskDetails, response.Metadata?.Kind);
        Assert.Equal(task.Id, response.Metadata?.TaskId);
        Assert.Equal(1, facade.StartAgentCallCount);
    }

    [Fact]
    public async Task HandleAsync_requires_group_continuation_feedback_to_reply_to_prompt_from_same_user()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Fix smoke test",
            "Update notes/status.txt.",
            Now);
        task.StartPlanning(Now);
        task.AdvanceIteration(Now);
        task.StartRunning(Now);
        task.StartReview(Now);
        var facade = new FakeTelegramApplicationFacade { Task = task };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var prompt = await handler.HandleAsync(
            new TelegramUpdate(
                -1001,
                CallbackData: TelegramCallbackData.ContinueTask(task.Id),
                SenderUserId: 1001,
                MessageId: 9001,
                MessageThreadId: 77,
                IsPrivateChat: false),
            CancellationToken.None);
        var otherUser = await handler.HandleAsync(
            new TelegramUpdate(
                -1001,
                Text: "Someone else tries to continue.",
                SenderUserId: 2002,
                MessageThreadId: 77,
                ReplyToMessageId: 9001,
                IsPrivateChat: false),
            CancellationToken.None);
        var notReply = await handler.HandleAsync(
            new TelegramUpdate(
                -1001,
                Text: "Same user but not a reply.",
                SenderUserId: 1001,
                MessageThreadId: 77,
                IsPrivateChat: false),
            CancellationToken.None);
        var response = await handler.HandleAsync(
            new TelegramUpdate(
                -1001,
                Text: "Please retry with sandbox disabled.",
                SenderUserId: 1001,
                MessageThreadId: 77,
                ReplyToMessageId: 9001,
                IsPrivateChat: false),
            CancellationToken.None);

        Assert.Equal("Reply to this message with the follow-up instructions for the next iteration.", prompt.Text);
        Assert.Contains("waiting for the user who pressed Continue", otherUser.Text, StringComparison.Ordinal);
        Assert.Contains("Reply to the bot follow-up prompt", notReply.Text, StringComparison.Ordinal);
        Assert.True(facade.ContinueTaskCalled);
        Assert.Equal("Please retry with sandbox disabled.", facade.ContinuedFeedback);
        Assert.Contains("Task continued: task-001", response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_completes_reviewing_task_from_button_callback()
    {
        var task = RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-aeges"),
            new MachineId("machine-local"),
            "Wire Telegram buttons",
            "Expose Telegram actions through inline buttons.",
            Now);
        task.StartPlanning(Now);
        task.StartRunning(Now);
        task.StartReview(Now);
        var facade = new FakeTelegramApplicationFacade { Task = task, QueuedTasks = [task] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.CompleteTask(task.Id)),
            CancellationToken.None);

        Assert.Equal("completed tasks:\n- task-001: Wire Telegram buttons", response.Text);
        Assert.Equal(RuntimeTaskStatus.Completed, task.Status);
        Assert.True(facade.CompleteTaskCalled);
        Assert.Equal("Wire Telegram buttons", response.Buttons.Rows[0][0].Text);
        Assert.Equal(TelegramCallbackData.ViewTask(task.Id), response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[1][0].Text);
        Assert.Equal(TelegramCallbackData.TaskMenu, response.Buttons.Rows[1][0].CallbackData);
        Assert.Equal(TelegramResponseKind.TaskWatch, response.Metadata?.Kind);
        Assert.Equal(task.Id, response.Metadata?.TaskId);
    }

    [Fact]
    public async Task HandleAsync_cancels_task_from_button_callback()
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
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.CancelTask(task.Id)),
            CancellationToken.None);

        Assert.Equal("Task cancelled: task-001", response.Text);
        Assert.Equal(RuntimeTaskStatus.Cancelled, task.Status);
        Assert.True(facade.CancelTaskCalled);
        AssertBackButton(response);
    }

    [Fact]
    public async Task HandleAsync_creates_task_from_button_guided_flow()
    {
        var project = RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now);
        var archivedProject = RuntimeProject.Create(new ProjectId("project-old"), "Old", "/workspace/old", Now);
        archivedProject.Archive(Now.AddMinutes(1));
        var machine = RuntimeMachine.Create(new MachineId("machine-local"), "Local", "macOS", Now);
        machine.MarkOnline(Now);
        var facade = new FakeTelegramApplicationFacade
        {
            Projects = [project, archivedProject],
            Machines = [machine],
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var projectResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.CreateTask),
            CancellationToken.None);
        var machineResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.SelectTaskProject(project.Id)),
            CancellationToken.None);
        var titleResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.SelectTaskMachine(machine.Id)),
            CancellationToken.None);
        var goalResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, Text: "Improve README"),
            CancellationToken.None);
        var createdResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, Text: "Add Telegram usage notes."),
            CancellationToken.None);

        Assert.Equal("Choose a project for the task.", projectResponse.Text);
        Assert.Equal("Aeges", projectResponse.Buttons.Rows[0][0].Text);
        Assert.DoesNotContain(
            projectResponse.Buttons.Rows.SelectMany(row => row),
            button => button.CallbackData == TelegramCallbackData.SelectTaskProject(archivedProject.Id));
        Assert.Equal("Choose the machine that should process the task.", machineResponse.Text);
        Assert.Equal("Local (online)", machineResponse.Buttons.Rows[0][0].Text);
        Assert.Equal("Send the task title.", titleResponse.Text);
        Assert.Equal("Now send the task goal/details.", goalResponse.Text);
        Assert.Contains("Task queued: task-created", createdResponse.Text, StringComparison.Ordinal);
        Assert.Contains("Agent:\n> Agent started.", createdResponse.Text, StringComparison.Ordinal);
        Assert.Equal(TelegramResponseKind.TaskDetails, createdResponse.Metadata?.Kind);
        Assert.Equal(1, facade.StartAgentCallCount);
        Assert.Equal(project.Id, facade.CreatedProjectId);
        Assert.Equal(machine.Id, facade.CreatedMachineId);
        Assert.Equal("Improve README", facade.CreatedTitle);
        Assert.Equal("Add Telegram usage notes.", facade.CreatedGoal);
    }

    [Fact]
    public async Task HandleAsync_cancels_task_creation_draft()
    {
        var project = RuntimeProject.Create(new ProjectId("project-aeges"), "Aeges", "/workspace/aeges", Now);
        var facade = new FakeTelegramApplicationFacade { Projects = [project] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.CreateTask),
            CancellationToken.None);
        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.CancelCreateTask),
            CancellationToken.None);

        Assert.Equal("Task creation cancelled.", response.Text);
        AssertBackButton(response);
    }

    [Fact]
    public async Task HandleAsync_lists_pending_approvals_with_detail_buttons()
    {
        var approval = ApprovalRequest.Create(
            new ApprovalId("approval-001"),
            new TaskId("task-001"),
            iterationId: null,
            "Dependency change requires approval.",
            "Modify package references.",
            Now);
        var facade = new FakeTelegramApplicationFacade { PendingApprovals = [approval] };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ListPendingApprovals),
            CancellationToken.None);

        Assert.Equal("Pending approvals:\n- approval-001: Modify package references.", response.Text);
        Assert.Equal("approval-001", response.Buttons.Rows[0][0].Text);
        Assert.Equal("ae:a:approval-001", response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Back", response.Buttons.Rows[1][0].Text);
    }

    [Fact]
    public async Task HandleAsync_shows_runner_settings_menu()
    {
        var facade = new FakeTelegramApplicationFacade
        {
            RunnerSettings = new TelegramRunnerSettings("workspace-write", CodexBypassApprovalsAndSandbox: false),
        };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.SettingsMenu),
            CancellationToken.None);

        Assert.Contains("Codex sandbox: workspace-write", response.Text, StringComparison.Ordinal);
        Assert.Contains("Codex bypass approvals and sandbox: disallowed", response.Text, StringComparison.Ordinal);
        Assert.Equal("Sandbox enabled", response.Buttons.Rows[0][0].Text);
        Assert.Equal("ae:s:sb:danger-full-access", response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal(TelegramButtonStyle.Success, response.Buttons.Rows[0][0].Style);
        Assert.Equal("Bypass disabled", response.Buttons.Rows[0][1].Text);
        Assert.Equal("ae:s:bp:1", response.Buttons.Rows[0][1].CallbackData);
        Assert.Equal(TelegramButtonStyle.Danger, response.Buttons.Rows[0][1].Style);
    }

    [Fact]
    public async Task HandleAsync_updates_runner_settings_from_buttons()
    {
        var facade = new FakeTelegramApplicationFacade();
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var sandboxResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.SetCodexSandboxMode("danger-full-access")),
            CancellationToken.None);
        var bypassResponse = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.SetCodexBypassApprovalsAndSandbox(true)),
            CancellationToken.None);

        Assert.Equal("danger-full-access", facade.RunnerSettings.CodexSandboxMode);
        Assert.True(facade.RunnerSettings.CodexBypassApprovalsAndSandbox);
        Assert.Equal(2, facade.RestartAgentCallCount);
        Assert.Contains("Codex sandbox: danger-full-access", sandboxResponse.Text, StringComparison.Ordinal);
        Assert.Contains("Codex bypass approvals and sandbox: allowed", bypassResponse.Text, StringComparison.Ordinal);
        Assert.Contains("Agent restart: Agent restarted.", sandboxResponse.Text, StringComparison.Ordinal);
        Assert.Contains("Agent restart: Agent restarted.", bypassResponse.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_shows_approval_details_with_resolution_buttons()
    {
        var approval = ApprovalRequest.Create(
            new ApprovalId("approval-001"),
            new TaskId("task-001"),
            iterationId: null,
            "Dependency change requires approval.",
            "Modify package references.",
            Now);
        var facade = new FakeTelegramApplicationFacade { Approval = approval };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ViewApproval(approval.Id)),
            CancellationToken.None);

        Assert.Contains("Approval: approval-001", response.Text, StringComparison.Ordinal);
        Assert.Contains("Task: task-001", response.Text, StringComparison.Ordinal);
        Assert.Contains("Status: pending", response.Text, StringComparison.Ordinal);
        Assert.Contains("Modify package references.", response.Text, StringComparison.Ordinal);
        Assert.Equal("Approve", response.Buttons.Rows[0][0].Text);
        Assert.Equal("ae:a:y:approval-001", response.Buttons.Rows[0][0].CallbackData);
        Assert.Equal("Reject", response.Buttons.Rows[0][1].Text);
        Assert.Equal("ae:a:n:approval-001", response.Buttons.Rows[0][1].CallbackData);
        Assert.Equal(TelegramCallbackData.ListPendingApprovals, response.Buttons.Rows[1][0].CallbackData);
    }

    [Fact]
    public async Task HandleAsync_approves_approval_from_button_callback()
    {
        var approval = ApprovalRequest.Create(
            new ApprovalId("approval-001"),
            new TaskId("task-001"),
            iterationId: null,
            "Dependency change requires approval.",
            "Modify package references.",
            Now);
        var facade = new FakeTelegramApplicationFacade { Approval = approval };
        var handler = new TelegramInteractionHandler(facade, new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: TelegramCallbackData.ApproveApproval(approval.Id)),
            CancellationToken.None);

        Assert.Equal("Approval approved: approval-001", response.Text);
        Assert.Equal("telegram:1001", facade.ResolvedBy);
        Assert.True(facade.ApproveCalled);
        AssertBackButton(response);
    }

    [Fact]
    public async Task HandleAsync_returns_menu_for_unknown_or_malformed_callbacks()
    {
        var handler = new TelegramInteractionHandler(new FakeTelegramApplicationFacade(), new AegesTelegramConfiguration());

        var response = await handler.HandleAsync(
            new TelegramUpdate(1001, CallbackData: "aeges:task:   "),
            CancellationToken.None);

        Assert.Equal("Unknown action. Send any message to open the Aeges menu.", response.Text);
        Assert.Empty(response.Buttons.Rows);
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

        public IReadOnlyList<RuntimeProjectGroup> ProjectGroups { get; init; } = [];

        public IReadOnlyList<RuntimeMachine> Machines { get; init; } = [];

        public IReadOnlyList<RuntimeTask> QueuedTasks { get; init; } = [];

        public RuntimeTask? Task { get; init; }

        public TelegramTaskReviewSnapshot? ReviewSnapshot { get; init; }

        public IReadOnlyList<ApprovalRequest> PendingApprovals { get; init; } = [];

        public ApprovalRequest? Approval { get; init; }

        public IReadOnlyList<RuntimeTelegramUser> TelegramUsers { get; init; } =
        [
            RuntimeTelegramUser.CreateFirstAdmin(1001, Now),
        ];

        public IReadOnlyList<RuntimeTelegramProjectAccess> TelegramProjectAccess { get; set; } = [];

        public IReadOnlyList<RuntimeTelegramProjectGroupAccess> TelegramProjectGroupAccess { get; set; } = [];

        public TelegramRunnerSettings RunnerSettings { get; set; } =
            new("workspace-write", CodexBypassApprovalsAndSandbox: false);

        public int ListProjectsCallCount { get; private set; }

        public ProjectId? CreatedProjectId { get; private set; }

        public ProjectId? ArchivedProjectId { get; private set; }

        public MachineId? CreatedMachineId { get; private set; }

        public string? CreatedTitle { get; private set; }

        public string? CreatedGoal { get; private set; }

        public string? TalkMessage { get; private set; }

        public bool ApproveCalled { get; private set; }

        public bool CancelTaskCalled { get; private set; }

        public bool CompleteTaskCalled { get; private set; }

        public bool ContinueTaskCalled { get; private set; }

        public string? ContinuedFeedback { get; private set; }

        public int RestartAgentCallCount { get; private set; }

        public int StartAgentCallCount { get; private set; }

        public string? ResolvedBy { get; private set; }

        public Task<TelegramUserAuthorization> EnsureTelegramUserAsync(
            long chatId,
            RuntimeTelegramUserProfile profile,
            CancellationToken cancellationToken)
        {
            var user = TelegramUsers.FirstOrDefault(user => user.ChatId == chatId)
                ?? RuntimeTelegramUser.CreateFirstAdmin(chatId, Now, profile);
            if (!profile.IsEmpty)
            {
                user.UpdateProfile(profile, Now);
            }

            return System.Threading.Tasks.Task.FromResult(new TelegramUserAuthorization(user, IsFirstAdmin: false));
        }

        public Task<IReadOnlyList<RuntimeTelegramUser>> ListTelegramUsersAsync(CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(TelegramUsers);

        public Task<ApplicationResult<TelegramUserAccessSnapshot>> GetTelegramUserAccessAsync(
            TelegramUserId userId,
            CancellationToken cancellationToken)
        {
            var user = TelegramUsers.FirstOrDefault(user => user.Id == userId);

            if (user is null)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<TelegramUserAccessSnapshot>.Failure(
                        "telegram_user_not_found",
                        $"Telegram user '{userId}' was not found."));
            }

            return System.Threading.Tasks.Task.FromResult(
                ApplicationResult<TelegramUserAccessSnapshot>.Success(
                    new TelegramUserAccessSnapshot(
                        user,
                        TelegramProjectAccess.Where(access => access.UserId == userId).ToArray(),
                        TelegramProjectGroupAccess.Where(access => access.UserId == userId).ToArray())));
        }

        public Task<ApplicationResult<RuntimeTelegramUser>> ApproveTelegramUserAsync(
            TelegramUserId userId,
            CancellationToken cancellationToken)
        {
            var user = TelegramUsers.FirstOrDefault(user => user.Id == userId);

            if (user is null)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeTelegramUser>.Failure(
                        "telegram_user_not_found",
                        $"Telegram user '{userId}' was not found."));
            }

            user.Approve(Now);

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<RuntimeTelegramUser>.Success(user));
        }

        public Task<ApplicationResult<RuntimeTelegramUser>> DenyTelegramUserAsync(
            TelegramUserId userId,
            CancellationToken cancellationToken)
        {
            var user = TelegramUsers.FirstOrDefault(user => user.Id == userId);

            if (user is null)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeTelegramUser>.Failure(
                        "telegram_user_not_found",
                        $"Telegram user '{userId}' was not found."));
            }

            user.Deny(Now);

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<RuntimeTelegramUser>.Success(user));
        }

        public Task<ApplicationResult<TelegramUserAccessSnapshot>> SetTelegramProjectAccessAsync(
            TelegramUserId userId,
            ProjectId projectId,
            bool allowed,
            CancellationToken cancellationToken)
        {
            var grants = TelegramProjectAccess
                .Where(access => access.UserId != userId || access.ProjectId != projectId)
                .ToList();

            if (allowed)
            {
                grants.Add(new RuntimeTelegramProjectAccess(userId, projectId, Now));
            }

            TelegramProjectAccess = grants;

            return GetTelegramUserAccessAsync(userId, cancellationToken);
        }

        public Task<ApplicationResult<TelegramUserAccessSnapshot>> SetTelegramProjectGroupAccessAsync(
            TelegramUserId userId,
            ProjectGroupId projectGroupId,
            bool allowed,
            CancellationToken cancellationToken)
        {
            var grants = TelegramProjectGroupAccess
                .Where(access => access.UserId != userId || access.ProjectGroupId != projectGroupId)
                .ToList();

            if (allowed)
            {
                grants.Add(new RuntimeTelegramProjectGroupAccess(userId, projectGroupId, Now));
            }

            TelegramProjectGroupAccess = grants;

            return GetTelegramUserAccessAsync(userId, cancellationToken);
        }

        public Task<IReadOnlyList<RuntimeProject>> ListProjectsAsync(CancellationToken cancellationToken)
        {
            ListProjectsCallCount++;
            return System.Threading.Tasks.Task.FromResult(Projects);
        }

        public Task<IReadOnlyList<RuntimeProjectGroup>> ListProjectGroupsAsync(CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(ProjectGroups);

        public Task<IReadOnlyList<RuntimeProject>> ListActiveProjectsAsync(CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(
                Projects.Where(project => !project.IsArchived).ToArray() as IReadOnlyList<RuntimeProject>);

        public Task<ApplicationResult<RuntimeProject>> GetProjectAsync(
            ProjectId projectId,
            CancellationToken cancellationToken)
        {
            var project = Projects.FirstOrDefault(project => project.Id == projectId);
            var result = project is null
                ? ApplicationResult<RuntimeProject>.Failure("project_not_found", $"Project '{projectId}' was not found.")
                : ApplicationResult<RuntimeProject>.Success(project);

            return System.Threading.Tasks.Task.FromResult(result);
        }

        public Task<ApplicationResult<RuntimeProject>> ArchiveProjectAsync(
            ProjectId projectId,
            CancellationToken cancellationToken)
        {
            ArchivedProjectId = projectId;
            var project = Projects.FirstOrDefault(project => project.Id == projectId);

            if (project is null)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeProject>.Failure("project_not_found", $"Project '{projectId}' was not found."));
            }

            project.Archive(Now);

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<RuntimeProject>.Success(project));
        }

        public Task<ApplicationResult<Aeges.Application.Talk.TalkExchange>> SendTalkMessageAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken)
        {
            TalkMessage = message;
            var session = RuntimeTalkSession.Create(
                new TalkSessionId("talk-001"),
                $"telegram:{chatId}",
                "Test talk",
                new RunnerId("codex"),
                Now);
            var userMessage = RuntimeTalkMessage.Create(
                new TalkMessageId("talk-message-user"),
                session.Id,
                TalkMessageRole.User,
                message,
                Now);
            var assistantMessage = RuntimeTalkMessage.Create(
                new TalkMessageId("talk-message-assistant"),
                session.Id,
                TalkMessageRole.Assistant,
                $"Talk response to: {message}",
                Now);

            return System.Threading.Tasks.Task.FromResult(
                ApplicationResult<Aeges.Application.Talk.TalkExchange>.Success(
                    new Aeges.Application.Talk.TalkExchange(session, userMessage, assistantMessage)));
        }

        public Task<IReadOnlyList<RuntimeMachine>> ListMachinesAsync(CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(Machines);

        public Task<IReadOnlyList<RuntimeTask>> ListQueuedTasksAsync(
            int limit,
            CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(QueuedTasks.Take(limit).ToArray() as IReadOnlyList<RuntimeTask>);

        public Task<IReadOnlyList<RuntimeTask>> ListTasksByStatusAsync(
            RuntimeTaskStatus status,
            int limit,
            CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(
                QueuedTasks.Where(task => task.Status == status).Take(limit).ToArray() as IReadOnlyList<RuntimeTask>);

        public Task<IReadOnlyList<RuntimeTask>> ListProjectTasksByStatusAsync(
            ProjectId projectId,
            RuntimeTaskStatus status,
            int limit,
            CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(
                QueuedTasks
                    .Where(task => task.ProjectId == projectId && task.Status == status)
                    .Take(limit)
                    .ToArray() as IReadOnlyList<RuntimeTask>);

        public Task<IReadOnlyList<ApprovalRequest>> ListPendingApprovalsAsync(
            int limit,
            CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(PendingApprovals.Take(limit).ToArray() as IReadOnlyList<ApprovalRequest>);

        public Task<TelegramRunnerSettings> GetRunnerSettingsAsync(CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(RunnerSettings);

        public Task<ApplicationResult<TelegramRunnerSettings>> SetCodexSandboxModeAsync(
            string sandboxMode,
            CancellationToken cancellationToken)
        {
            RunnerSettings = RunnerSettings with
            {
                CodexSandboxMode = sandboxMode,
            };

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<TelegramRunnerSettings>.Success(RunnerSettings));
        }

        public Task<ApplicationResult<TelegramRunnerSettings>> SetCodexBypassApprovalsAndSandboxAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            RunnerSettings = RunnerSettings with
            {
                CodexBypassApprovalsAndSandbox = enabled,
            };

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<TelegramRunnerSettings>.Success(RunnerSettings));
        }

        public Task<ApplicationResult<TelegramAgentRestartResult>> RestartAgentAsync(CancellationToken cancellationToken)
        {
            RestartAgentCallCount++;

            return System.Threading.Tasks.Task.FromResult(
                ApplicationResult<TelegramAgentRestartResult>.Success(
                    new TelegramAgentRestartResult("Agent restarted.")));
        }

        public Task<ApplicationResult<TelegramAgentRestartResult>> StartAgentAsync(CancellationToken cancellationToken)
        {
            StartAgentCallCount++;

            return System.Threading.Tasks.Task.FromResult(
                ApplicationResult<TelegramAgentRestartResult>.Success(
                    new TelegramAgentRestartResult("Agent started.")));
        }

        public Task<ApplicationResult<RuntimeTask>> CreateTaskAsync(
            ProjectId projectId,
            MachineId machineId,
            string title,
            string goal,
            CancellationToken cancellationToken)
        {
            CreatedProjectId = projectId;
            CreatedMachineId = machineId;
            CreatedTitle = title;
            CreatedGoal = goal;
            var task = RuntimeTask.Create(
                new TaskId("task-created"),
                projectId,
                machineId,
                title,
                goal,
                Now);

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<RuntimeTask>.Success(task));
        }

        public Task<ApplicationResult<RuntimeTask>> GetTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            var result = Task is not null && Task.Id == taskId
                ? ApplicationResult<RuntimeTask>.Success(Task)
                : ApplicationResult<RuntimeTask>.Failure("task_not_found", $"Task '{taskId}' was not found.");

            return System.Threading.Tasks.Task.FromResult(result);
        }

        public Task<ApplicationResult<TelegramTaskReviewSnapshot>> GetTaskReviewAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            if (ReviewSnapshot is not null && ReviewSnapshot.Task.Id == taskId)
            {
                return System.Threading.Tasks.Task.FromResult(ApplicationResult<TelegramTaskReviewSnapshot>.Success(ReviewSnapshot));
            }

            var result = Task is not null && Task.Id == taskId
                ? ApplicationResult<TelegramTaskReviewSnapshot>.Success(
                    new TelegramTaskReviewSnapshot(Task, [], [], "/runtime/artifacts", [], LatestRunnerResponse: null))
                : ApplicationResult<TelegramTaskReviewSnapshot>.Failure("task_not_found", $"Task '{taskId}' was not found.");

            return System.Threading.Tasks.Task.FromResult(result);
        }

        public Task<ApplicationResult<RuntimeTask>> CancelTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            CancelTaskCalled = true;

            if (Task is null || Task.Id != taskId)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeTask>.Failure("task_not_found", $"Task '{taskId}' was not found."));
            }

            Task.Cancel(Now);
            return System.Threading.Tasks.Task.FromResult(ApplicationResult<RuntimeTask>.Success(Task));
        }

        public Task<ApplicationResult<RuntimeTask>> CompleteTaskAsync(
            TaskId taskId,
            CancellationToken cancellationToken)
        {
            CompleteTaskCalled = true;

            if (Task is null || Task.Id != taskId)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeTask>.Failure("task_not_found", $"Task '{taskId}' was not found."));
            }

            try
            {
                Task.Complete(Now);
            }
            catch (AegesDomainException exception)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeTask>.Failure("invalid_task_status_transition", exception.Message));
            }

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<RuntimeTask>.Success(Task));
        }

        public Task<ApplicationResult<RuntimeTask>> ContinueTaskAsync(
            TaskId taskId,
            string feedback,
            CancellationToken cancellationToken)
        {
            ContinueTaskCalled = true;
            ContinuedFeedback = feedback;

            if (Task is null || Task.Id != taskId)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeTask>.Failure("task_not_found", $"Task '{taskId}' was not found."));
            }

            try
            {
                Task.RequeueForRevision(Now);
            }
            catch (AegesDomainException exception)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<RuntimeTask>.Failure("task_domain_rule_violation", exception.Message));
            }

            return System.Threading.Tasks.Task.FromResult(ApplicationResult<RuntimeTask>.Success(Task));
        }

        public Task<ApplicationResult<ApprovalRequest>> GetApprovalAsync(
            ApprovalId approvalId,
            CancellationToken cancellationToken)
        {
            var result = Approval is not null && Approval.Id == approvalId
                ? ApplicationResult<ApprovalRequest>.Success(Approval)
                : ApplicationResult<ApprovalRequest>.Failure(
                    "approval_not_found",
                    $"Approval request '{approvalId}' was not found.");

            return System.Threading.Tasks.Task.FromResult(result);
        }

        public Task<ApplicationResult<ApprovalRequest>> ApproveApprovalAsync(
            ApprovalId approvalId,
            string resolvedBy,
            CancellationToken cancellationToken)
        {
            ApproveCalled = true;
            ResolvedBy = resolvedBy;

            if (Approval is null || Approval.Id != approvalId)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<ApprovalRequest>.Failure(
                        "approval_not_found",
                        $"Approval request '{approvalId}' was not found."));
            }

            Approval.Approve(resolvedBy, Now);
            return System.Threading.Tasks.Task.FromResult(ApplicationResult<ApprovalRequest>.Success(Approval));
        }

        public Task<ApplicationResult<ApprovalRequest>> RejectApprovalAsync(
            ApprovalId approvalId,
            string resolvedBy,
            CancellationToken cancellationToken)
        {
            ResolvedBy = resolvedBy;

            if (Approval is null || Approval.Id != approvalId)
            {
                return System.Threading.Tasks.Task.FromResult(
                    ApplicationResult<ApprovalRequest>.Failure(
                        "approval_not_found",
                        $"Approval request '{approvalId}' was not found."));
            }

            Approval.Reject(resolvedBy, Now);
            return System.Threading.Tasks.Task.FromResult(ApplicationResult<ApprovalRequest>.Success(Approval));
        }
    }
}
