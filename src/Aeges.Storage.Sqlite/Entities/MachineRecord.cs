using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for a machine registered with the local runtime.
/// </summary>
internal sealed class MachineRecord
{
    /// <summary>
    /// Gets or sets the machine identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable machine name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine platform description.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine availability status.
    /// </summary>
    public MachineStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the machine last reported a heartbeat.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>
    /// Gets or sets the registration timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets the tasks assigned to this machine.
    /// </summary>
    public ICollection<TaskRecord> Tasks { get; } = [];

    /// <summary>
    /// Gets the runtime events associated with this machine.
    /// </summary>
    public ICollection<RuntimeEventRecord> RuntimeEvents { get; } = [];
}
