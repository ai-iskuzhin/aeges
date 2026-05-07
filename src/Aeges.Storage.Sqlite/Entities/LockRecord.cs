namespace Aeges.Storage.Sqlite.Entities;

internal sealed class LockRecord
{
    public string Id { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string PathPattern { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public TaskRecord? Task { get; set; }

    public ProjectRecord? Project { get; set; }
}
