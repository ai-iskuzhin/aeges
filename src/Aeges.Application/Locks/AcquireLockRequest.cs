using Aeges.Core;

namespace Aeges.Application.Locks;

/// <summary>
/// Describes a request to acquire a path-based repository lock for a task.
/// </summary>
/// <param name="TaskId">The task acquiring the lock.</param>
/// <param name="ProjectId">The project where the lock applies.</param>
/// <param name="PathPattern">The relative path pattern protected by the lock.</param>
/// <param name="LockId">The optional lock identifier. A new identifier is generated when omitted.</param>
public sealed record AcquireLockRequest(
    TaskId TaskId,
    ProjectId ProjectId,
    string PathPattern,
    LockId? LockId = null);
