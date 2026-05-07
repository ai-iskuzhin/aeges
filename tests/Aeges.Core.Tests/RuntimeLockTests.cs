using Aeges.Core;

namespace Aeges.Core.Tests;

public sealed class RuntimeLockTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Acquire_initializes_active_lock()
    {
        var runtimeLock = CreateLock("src/Auth/*");

        Assert.Equal(new LockId("lock-001"), runtimeLock.Id);
        Assert.Equal(new TaskId("task-001"), runtimeLock.TaskId);
        Assert.Equal(new ProjectId("project-001"), runtimeLock.ProjectId);
        Assert.Equal("src/Auth/*", runtimeLock.PathPattern);
        Assert.Equal(CreatedAt, runtimeLock.CreatedAt);
        Assert.True(runtimeLock.IsActive);
        Assert.Null(runtimeLock.ReleasedAt);
    }

    [Fact]
    public void Rehydrate_restores_released_lock()
    {
        var releasedAt = CreatedAt.AddMinutes(1);

        var runtimeLock = RuntimeLock.Rehydrate(
            new LockId("lock-001"),
            new TaskId("task-001"),
            new ProjectId("project-001"),
            "src/Auth/*",
            CreatedAt,
            releasedAt);

        Assert.Equal(new LockId("lock-001"), runtimeLock.Id);
        Assert.Equal("src/Auth/*", runtimeLock.PathPattern);
        Assert.Equal(CreatedAt, runtimeLock.CreatedAt);
        Assert.Equal(releasedAt, runtimeLock.ReleasedAt);
        Assert.False(runtimeLock.IsActive);
    }

    [Fact]
    public void Release_marks_lock_inactive_once()
    {
        var runtimeLock = CreateLock("src/Auth/*");
        var releasedAt = CreatedAt.AddMinutes(1);

        runtimeLock.Release(releasedAt);

        Assert.False(runtimeLock.IsActive);
        Assert.Equal(releasedAt, runtimeLock.ReleasedAt);
        Assert.Throws<AegesDomainException>(() => runtimeLock.Release(releasedAt.AddMinutes(1)));
    }

    [Theory]
    [InlineData("/absolute/path")]
    [InlineData("C:\\absolute\\path")]
    [InlineData("\\\\server\\share\\path")]
    [InlineData("../outside")]
    [InlineData("src/../outside")]
    public void Acquire_rejects_unsafe_path_patterns(string pathPattern)
    {
        Assert.Throws<ArgumentException>(() => CreateLock(pathPattern));
    }

    private static RuntimeLock CreateLock(string pathPattern) =>
        RuntimeLock.Acquire(
            new LockId("lock-001"),
            new TaskId("task-001"),
            new ProjectId("project-001"),
            pathPattern,
            CreatedAt);
}
