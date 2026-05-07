namespace Aeges.Core;

/// <summary>
/// Represents durable metadata for an artifact stored outside the database.
/// </summary>
public sealed class RuntimeArtifact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifact"/> class.
    /// </summary>
    /// <param name="id">The artifact identifier.</param>
    /// <param name="taskId">The task that owns the artifact.</param>
    /// <param name="iterationId">The iteration that produced or consumed the artifact, when applicable.</param>
    /// <param name="type">The artifact type.</param>
    /// <param name="relativePath">The artifact path relative to the configured artifact root.</param>
    /// <param name="createdAt">The artifact creation timestamp.</param>
    /// <param name="sizeBytes">The artifact size in bytes, when known.</param>
    /// <param name="sha256">The artifact SHA-256 checksum, when known.</param>
    public RuntimeArtifact(
        ArtifactId id,
        TaskId taskId,
        IterationId? iterationId,
        ArtifactType type,
        string relativePath,
        DateTimeOffset createdAt,
        long? sizeBytes = null,
        string? sha256 = null)
    {
        Id = id;
        TaskId = taskId;
        IterationId = iterationId;
        Type = type;
        RelativePath = ArtifactPath.RequireRelative(relativePath);
        CreatedAt = createdAt;
        SizeBytes = RequireNonNegative(sizeBytes, nameof(sizeBytes));
        Sha256 = RequireSha256(sha256, nameof(sha256));
    }

    /// <summary>
    /// Gets the artifact identifier.
    /// </summary>
    public ArtifactId Id { get; }

    /// <summary>
    /// Gets the task that owns the artifact.
    /// </summary>
    public TaskId TaskId { get; }

    /// <summary>
    /// Gets the iteration that produced or consumed the artifact, when applicable.
    /// </summary>
    public IterationId? IterationId { get; }

    /// <summary>
    /// Gets the artifact type.
    /// </summary>
    public ArtifactType Type { get; }

    /// <summary>
    /// Gets the artifact path relative to the configured artifact root.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the artifact size in bytes, when known.
    /// </summary>
    public long? SizeBytes { get; }

    /// <summary>
    /// Gets the artifact SHA-256 checksum, when known.
    /// </summary>
    public string? Sha256 { get; }

    /// <summary>
    /// Gets the artifact creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    private static long? RequireNonNegative(long? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must not be negative.");
        }

        return value;
    }

    private static string? RequireSha256(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("SHA-256 checksum must be a 64-character hexadecimal value.", parameterName);
        }

        return value.ToLowerInvariant();
    }
}

internal static class ArtifactPath
{
    public static string RequireRelative(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Artifact relative path must not be empty.", nameof(value));
        }

        if (Path.IsPathRooted(value) || HasWindowsRoot(value))
        {
            throw new ArgumentException("Artifact path must be relative.", nameof(value));
        }

        var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Artifact path must not contain current or parent directory segments.", nameof(value));
        }

        return value;
    }

    private static bool HasWindowsRoot(string value) =>
        value.StartsWith(@"\\", StringComparison.Ordinal)
        || (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/');
}
