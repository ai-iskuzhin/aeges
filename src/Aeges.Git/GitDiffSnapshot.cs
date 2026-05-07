namespace Aeges.Git;

/// <summary>
/// Describes a captured Git diff snapshot.
/// </summary>
/// <param name="RepositoryPath">The repository path.</param>
/// <param name="DiffText">The captured diff text.</param>
/// <param name="ChangedPaths">The repository-relative changed paths.</param>
/// <param name="CapturedAt">The capture timestamp.</param>
public sealed record GitDiffSnapshot(
    string RepositoryPath,
    string DiffText,
    IReadOnlyList<string> ChangedPaths,
    DateTimeOffset CapturedAt);
