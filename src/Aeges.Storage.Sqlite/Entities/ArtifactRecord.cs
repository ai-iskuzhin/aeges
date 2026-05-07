namespace Aeges.Storage.Sqlite.Entities;

internal sealed class ArtifactRecord
{
    public string Id { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string? IterationId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public long? SizeBytes { get; set; }

    public string? Sha256 { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public TaskRecord? Task { get; set; }

    public TaskIterationRecord? Iteration { get; set; }
}
