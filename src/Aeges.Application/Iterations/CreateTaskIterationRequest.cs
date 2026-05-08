using Aeges.Core;

namespace Aeges.Application.Iterations;

/// <summary>
/// Describes a request to create the next bounded iteration for a task.
/// </summary>
/// <param name="TaskId">The task identifier.</param>
/// <param name="RunnerId">The runner assigned to execute the iteration.</param>
/// <param name="IterationId">The optional caller-provided iteration identifier.</param>
public sealed record CreateTaskIterationRequest(
    TaskId TaskId,
    RunnerId RunnerId,
    IterationId? IterationId = null);
