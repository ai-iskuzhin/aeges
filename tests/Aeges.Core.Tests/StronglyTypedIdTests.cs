using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class StronglyTypedIdTests
{
    [Fact]
    public void TaskId_rejects_empty_value()
    {
        Assert.Throws<ArgumentException>(() => new TaskId(" "));
    }

    [Fact]
    public void Strongly_typed_ids_preserve_value()
    {
        var taskId = new TaskId("task-001");

        Assert.Equal("task-001", taskId.Value);
        Assert.Equal("task-001", taskId.ToString());
    }

    [Fact]
    public void Generated_ids_include_type_prefix()
    {
        Assert.StartsWith("task-", TaskId.New().Value);
        Assert.StartsWith("project-", ProjectId.New().Value);
        Assert.StartsWith("machine-", MachineId.New().Value);
        Assert.StartsWith("iteration-", IterationId.New().Value);
        Assert.StartsWith("artifact-", ArtifactId.New().Value);
        Assert.StartsWith("approval-", ApprovalId.New().Value);
        Assert.StartsWith("runner-", RunnerId.New().Value);
        Assert.StartsWith("runner-execution-", RunnerExecutionId.New().Value);
        Assert.StartsWith("lock-", LockId.New().Value);
    }
}
