namespace Aeges.Core;

/// <summary>
/// Represents an attempt to move a task iteration through an unsupported lifecycle transition.
/// </summary>
public sealed class InvalidTaskIterationStatusTransitionException : AegesDomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTaskIterationStatusTransitionException"/> class.
    /// </summary>
    /// <param name="from">The current iteration status.</param>
    /// <param name="to">The requested next iteration status.</param>
    public InvalidTaskIterationStatusTransitionException(TaskIterationStatus from, TaskIterationStatus to)
        : base($"Iteration status transition from '{from.ToStorageValue()}' to '{to.ToStorageValue()}' is not allowed.")
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// Gets the status the iteration was in before the rejected transition.
    /// </summary>
    public TaskIterationStatus From { get; }

    /// <summary>
    /// Gets the requested status that was rejected.
    /// </summary>
    public TaskIterationStatus To { get; }
}
