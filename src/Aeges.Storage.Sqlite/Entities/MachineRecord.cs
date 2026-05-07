using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

internal sealed class MachineRecord
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public MachineStatus Status { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TaskRecord> Tasks { get; } = [];

    public ICollection<RuntimeEventRecord> RuntimeEvents { get; } = [];
}
