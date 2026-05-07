namespace Aeges.Storage.Sqlite.Entities;

internal sealed class ProjectRecord
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TaskRecord> Tasks { get; } = [];

    public ICollection<LockRecord> Locks { get; } = [];
}
