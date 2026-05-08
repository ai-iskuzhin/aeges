using Aeges.Application.RunnerDispatch;
using Aeges.Application.Runtime;
using Aeges.Core;

namespace Aeges.Application.Tests;

public sealed class RunnerRequestFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 08, 14, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_builds_deterministic_runner_request_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "aeges-runner-request-tests");
        var layout = RuntimeDirectoryLayout.Create(root);
        var task = CreateTask();
        var project = RuntimeProject.Create(new ProjectId("project-001"), "Aeges", "/work/aeges", Now);
        var iteration = TaskIteration.Create(
            new IterationId("iteration-001"),
            task.Id,
            1,
            new RunnerId("codex"),
            Now);
        var factory = new RunnerRequestFactory();

        var result = factory.Create(
            new CreateRunnerRequestRequest(
                task,
                project,
                iteration,
                layout,
                TimeSpan.FromMinutes(30),
                new Dictionary<string, string> { ["AEGES_TEST"] = "1" },
                new Dictionary<string, string> { ["maxChangedFiles"] = "20" }));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(task.Id, result.Value.TaskId);
        Assert.Equal(iteration.Id, result.Value.IterationId);
        Assert.Equal(project.Id, result.Value.ProjectId);
        Assert.Equal(project.Path, result.Value.ProjectPath);
        Assert.Equal(
            Path.Combine(root, "worktrees", "project-001", "task-001", "iteration-001"),
            result.Value.WorktreePath);
        Assert.Equal(
            Path.Combine(root, "artifacts", "project-001", "task-001", "iteration-001"),
            result.Value.ArtifactOutputDirectory);
        Assert.Equal(
            Path.Combine(root, "artifacts", "project-001", "task-001", "iteration-001", "prompt.md"),
            result.Value.PromptPath);
        Assert.Equal(TimeSpan.FromMinutes(30), result.Value.Timeout);
        Assert.Equal("1", result.Value.EnvironmentVariables["AEGES_TEST"]);
        Assert.Equal("20", result.Value.PolicyHints["maxChangedFiles"]);
    }

    [Fact]
    public void Create_rejects_project_mismatch()
    {
        var task = CreateTask();
        var project = RuntimeProject.Create(new ProjectId("other-project"), "Other", "/work/other", Now);
        var iteration = TaskIteration.Create(new IterationId("iteration-001"), task.Id, 1, new RunnerId("codex"), Now);
        var factory = new RunnerRequestFactory();

        var result = factory.Create(
            new CreateRunnerRequestRequest(
                task,
                project,
                iteration,
                RuntimeDirectoryLayout.Create(Path.Combine(Path.GetTempPath(), "aeges-runner-request-tests")),
                TimeSpan.FromMinutes(30)));

        Assert.False(result.IsSuccess);
        Assert.Equal("project_mismatch", result.Error?.Code);
    }

    [Fact]
    public void Create_rejects_iteration_mismatch()
    {
        var task = CreateTask();
        var project = RuntimeProject.Create(task.ProjectId, "Aeges", "/work/aeges", Now);
        var iteration = TaskIteration.Create(
            new IterationId("iteration-001"),
            new TaskId("other-task"),
            1,
            new RunnerId("codex"),
            Now);
        var factory = new RunnerRequestFactory();

        var result = factory.Create(
            new CreateRunnerRequestRequest(
                task,
                project,
                iteration,
                RuntimeDirectoryLayout.Create(Path.Combine(Path.GetTempPath(), "aeges-runner-request-tests")),
                TimeSpan.FromMinutes(30)));

        Assert.False(result.IsSuccess);
        Assert.Equal("iteration_mismatch", result.Error?.Code);
    }

    private static RuntimeTask CreateTask() =>
        RuntimeTask.Create(
            new TaskId("task-001"),
            new ProjectId("project-001"),
            new MachineId("machine-001"),
            "Run Codex",
            "Execute a governed iteration.",
            Now);
}
