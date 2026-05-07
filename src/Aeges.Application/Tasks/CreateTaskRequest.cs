using Aeges.Core;

namespace Aeges.Application.Tasks;

/// <summary>
/// Describes a request to create a durable runtime task.
/// </summary>
/// <param name="ProjectId">The project that owns the task.</param>
/// <param name="MachineId">The machine assigned to the task.</param>
/// <param name="Title">The human-readable task title.</param>
/// <param name="Goal">The task goal.</param>
/// <param name="Priority">The task scheduling priority.</param>
/// <param name="MaxIterations">The maximum number of allowed task iterations.</param>
/// <param name="TaskId">The optional task identifier. A new identifier is generated when omitted.</param>
public sealed record CreateTaskRequest(
    ProjectId ProjectId,
    MachineId MachineId,
    string Title,
    string Goal,
    int Priority = 0,
    int MaxIterations = 3,
    TaskId? TaskId = null);
