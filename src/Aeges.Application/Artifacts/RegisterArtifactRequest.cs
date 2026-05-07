using Aeges.Core;

namespace Aeges.Application.Artifacts;

/// <summary>
/// Describes a request to register durable artifact metadata.
/// </summary>
/// <param name="TaskId">The task that owns the artifact.</param>
/// <param name="IterationId">The related iteration, when applicable.</param>
/// <param name="Type">The artifact type.</param>
/// <param name="RelativePath">The artifact path relative to the artifact root.</param>
/// <param name="SizeBytes">The artifact size in bytes, when known.</param>
/// <param name="Sha256">The artifact SHA-256 checksum, when known.</param>
/// <param name="ArtifactId">The optional artifact identifier. A new identifier is generated when omitted.</param>
public sealed record RegisterArtifactRequest(
    TaskId TaskId,
    IterationId? IterationId,
    ArtifactType Type,
    string RelativePath,
    long? SizeBytes = null,
    string? Sha256 = null,
    ArtifactId? ArtifactId = null);
