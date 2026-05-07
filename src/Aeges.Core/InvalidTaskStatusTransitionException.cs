namespace Aeges.Core;

/// <summary>
/// Represents an attempt to move a runtime task through an unsupported lifecycle transition.
/// </summary>
public sealed class InvalidTaskStatusTransitionException : AegesDomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTaskStatusTransitionException"/> class.
    /// </summary>
    /// <param name="from">The current task status.</param>
    /// <param name="to">The requested next task status.</param>
    public InvalidTaskStatusTransitionException(RuntimeTaskStatus from, RuntimeTaskStatus to)
        : base($"Task status transition from '{from.ToStorageValue()}' to '{to.ToStorageValue()}' is not allowed.")
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// Gets the status the task was in before the rejected transition.
    /// </summary>
    public RuntimeTaskStatus From { get; }

    /// <summary>
    /// Gets the requested status that was rejected.
    /// </summary>
    public RuntimeTaskStatus To { get; }
}
