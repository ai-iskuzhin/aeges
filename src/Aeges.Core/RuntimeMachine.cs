namespace Aeges.Core;

/// <summary>
/// Represents a machine registered with the Aeges runtime.
/// </summary>
public sealed class RuntimeMachine
{
    private RuntimeMachine(MachineId id, string name, string platform, DateTimeOffset createdAt)
    {
        Id = id;
        Name = RequireText(name, nameof(name));
        Platform = RequireText(platform, nameof(platform));
        Status = MachineStatus.Offline;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>
    /// Gets the machine identifier.
    /// </summary>
    public MachineId Id { get; }

    /// <summary>
    /// Gets the human-readable machine name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the machine platform description.
    /// </summary>
    public string Platform { get; private set; }

    /// <summary>
    /// Gets the machine availability status.
    /// </summary>
    public MachineStatus Status { get; private set; }

    /// <summary>
    /// Gets the timestamp when the machine was last seen by the runtime.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the machine was registered.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when machine metadata was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a new registered machine.
    /// </summary>
    /// <param name="id">The machine identifier.</param>
    /// <param name="name">The human-readable machine name.</param>
    /// <param name="platform">The machine platform description.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <returns>A registered runtime machine.</returns>
    public static RuntimeMachine Create(MachineId id, string name, string platform, DateTimeOffset createdAt) =>
        new(id, name, platform, createdAt);

    /// <summary>
    /// Rehydrates a registered machine from durable storage.
    /// </summary>
    /// <param name="id">The machine identifier.</param>
    /// <param name="name">The human-readable machine name.</param>
    /// <param name="platform">The machine platform description.</param>
    /// <param name="status">The machine availability status.</param>
    /// <param name="lastSeenAt">The timestamp when the machine was last seen by the runtime.</param>
    /// <param name="createdAt">The registration timestamp.</param>
    /// <param name="updatedAt">The last update timestamp.</param>
    /// <returns>A rehydrated runtime machine.</returns>
    public static RuntimeMachine Rehydrate(
        MachineId id,
        string name,
        string platform,
        MachineStatus status,
        DateTimeOffset? lastSeenAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var machine = new RuntimeMachine(id, name, platform, createdAt)
        {
            Status = status,
            LastSeenAt = lastSeenAt,
            UpdatedAt = updatedAt,
        };

        return machine;
    }

    /// <summary>
    /// Updates machine metadata.
    /// </summary>
    /// <param name="name">The new machine name.</param>
    /// <param name="platform">The new machine platform description.</param>
    /// <param name="now">The update timestamp.</param>
    public void Update(string name, string platform, DateTimeOffset now)
    {
        Name = RequireText(name, nameof(name));
        Platform = RequireText(platform, nameof(platform));
        UpdatedAt = now;
    }

    /// <summary>
    /// Records a heartbeat and marks the machine online.
    /// </summary>
    /// <param name="now">The heartbeat timestamp.</param>
    public void MarkOnline(DateTimeOffset now)
    {
        Status = MachineStatus.Online;
        LastSeenAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Marks the machine as currently executing work.
    /// </summary>
    /// <param name="now">The status update timestamp.</param>
    public void MarkBusy(DateTimeOffset now)
    {
        Status = MachineStatus.Busy;
        LastSeenAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Marks the machine offline.
    /// </summary>
    /// <param name="now">The status update timestamp.</param>
    public void MarkOffline(DateTimeOffset now)
    {
        Status = MachineStatus.Offline;
        UpdatedAt = now;
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }
}
