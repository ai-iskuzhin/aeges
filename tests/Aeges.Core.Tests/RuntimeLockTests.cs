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
    [InlineData("src/Auth/*", "src/Auth/Login.cs", true)]
    [InlineData("src/Auth/*", "src/Payments/Checkout.cs", false)]
    [InlineData("src/**", "src/Auth/Login.cs", true)]
    [InlineData("package.json", "package.json", true)]
    public void ProtectsPath_matches_relative_paths(string pattern, string path, bool expected)
    {
        var runtimeLock = CreateLock(pattern);

        Assert.Equal(expected, runtimeLock.ProtectsPath(path));
    }

    [Fact]
    public void ConflictsWith_detects_overlapping_active_locks_for_different_tasks()
    {
        var left = CreateLock("src/Auth/*");
        var right = RuntimeLock.Acquire(
            new LockId("lock-002"),
            new TaskId("task-002"),
            new ProjectId("project-001"),
            "src/Auth/Login.cs",
            CreatedAt);

        Assert.True(left.ConflictsWith(right));
        Assert.True(right.ConflictsWith(left));
    }

    [Fact]
    public void ConflictsWith_ignores_same_task_different_project_and_released_locks()
    {
        var left = CreateLock("src/Auth/*");
        var sameTask = RuntimeLock.Acquire(new LockId("lock-002"), new TaskId("task-001"), new ProjectId("project-001"), "src/Auth/Login.cs", CreatedAt);
        var differentProject = RuntimeLock.Acquire(new LockId("lock-003"), new TaskId("task-002"), new ProjectId("project-002"), "src/Auth/Login.cs", CreatedAt);
        var released = RuntimeLock.Acquire(new LockId("lock-004"), new TaskId("task-002"), new ProjectId("project-001"), "src/Auth/Login.cs", CreatedAt);
        released.Release(CreatedAt.AddMinutes(1));

        Assert.False(left.ConflictsWith(sameTask));
        Assert.False(left.ConflictsWith(differentProject));
        Assert.False(left.ConflictsWith(released));
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
