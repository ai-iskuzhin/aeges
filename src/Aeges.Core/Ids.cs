namespace Aeges.Core;

/// <summary>
/// Identifies a durable runtime task.
/// </summary>
public readonly record struct TaskId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskId"/> struct.
    /// </summary>
    /// <param name="value">The stable task identifier value.</param>
    public TaskId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new task identifier.
    /// </summary>
    /// <returns>A generated task identifier.</returns>
    public static TaskId New() => new(IdValue.New("task"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies a registered project.
/// </summary>
public readonly record struct ProjectId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectId"/> struct.
    /// </summary>
    /// <param name="value">The stable project identifier value.</param>
    public ProjectId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new project identifier.
    /// </summary>
    /// <returns>A generated project identifier.</returns>
    public static ProjectId New() => new(IdValue.New("project"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies a machine that can host or execute governed runtime work.
/// </summary>
public readonly record struct MachineId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MachineId"/> struct.
    /// </summary>
    /// <param name="value">The stable machine identifier value.</param>
    public MachineId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new machine identifier.
    /// </summary>
    /// <returns>A generated machine identifier.</returns>
    public static MachineId New() => new(IdValue.New("machine"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies one bounded execution iteration for a task.
/// </summary>
public readonly record struct IterationId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IterationId"/> struct.
    /// </summary>
    /// <param name="value">The stable iteration identifier value.</param>
    public IterationId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new iteration identifier.
    /// </summary>
    /// <returns>A generated iteration identifier.</returns>
    public static IterationId New() => new(IdValue.New("iteration"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies a durable artifact produced or consumed by the runtime.
/// </summary>
public readonly record struct ArtifactId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactId"/> struct.
    /// </summary>
    /// <param name="value">The stable artifact identifier value.</param>
    public ArtifactId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new artifact identifier.
    /// </summary>
    /// <returns>A generated artifact identifier.</returns>
    public static ArtifactId New() => new(IdValue.New("artifact"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies an approval request in a governed task workflow.
/// </summary>
public readonly record struct ApprovalId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalId"/> struct.
    /// </summary>
    /// <param name="value">The stable approval identifier value.</param>
    public ApprovalId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new approval identifier.
    /// </summary>
    /// <returns>A generated approval identifier.</returns>
    public static ApprovalId New() => new(IdValue.New("approval"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies an interchangeable task runner implementation.
/// </summary>
public readonly record struct RunnerId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunnerId"/> struct.
    /// </summary>
    /// <param name="value">The stable runner identifier value.</param>
    public RunnerId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new runner identifier.
    /// </summary>
    /// <returns>A generated runner identifier.</returns>
    public static RunnerId New() => new(IdValue.New("runner"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies a repository lock held for governed task execution.
/// </summary>
public readonly record struct LockId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LockId"/> struct.
    /// </summary>
    /// <param name="value">The stable lock identifier value.</param>
    public LockId(string value)
    {
        Value = IdValue.Require(value);
    }

    /// <summary>
    /// Gets the stable identifier value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new lock identifier.
    /// </summary>
    /// <returns>A generated lock identifier.</returns>
    public static LockId New() => new(IdValue.New("lock"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

internal static class IdValue
{
    public static string New(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static string Require(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier value must not be empty.", nameof(value));
        }

        return value;
    }
}
