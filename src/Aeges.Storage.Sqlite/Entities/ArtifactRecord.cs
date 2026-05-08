using Aeges.Core;

namespace Aeges.Storage.Sqlite.Entities;

/// <summary>
/// EF Core persistence record for artifact metadata.
/// </summary>
internal sealed class ArtifactRecord
{
    /// <summary>
    /// Gets or sets the artifact identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning task identifier.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the producing or consuming iteration identifier, when applicable.
    /// </summary>
    public string? IterationId { get; set; }

    /// <summary>
    /// Gets or sets the artifact category.
    /// </summary>
    public ArtifactType Type { get; set; }

    /// <summary>
    /// Gets or sets the artifact path relative to the configured artifact root.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact size in bytes, when known.
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the artifact SHA-256 checksum, when known.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// Gets or sets the artifact creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning task navigation.
    /// </summary>
    public TaskRecord? Task { get; set; }

    /// <summary>
    /// Gets or sets the related iteration navigation.
    /// </summary>
    public TaskIterationRecord? Iteration { get; set; }
}
