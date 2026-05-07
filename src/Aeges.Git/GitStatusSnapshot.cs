namespace Aeges.Git;

/// <summary>
/// Describes a captured Git status snapshot.
/// </summary>
/// <param name="RepositoryPath">The repository path.</param>
/// <param name="Entries">The changed status entries.</param>
/// <param name="CapturedAt">The capture timestamp.</param>
public sealed record GitStatusSnapshot(
    string RepositoryPath,
    IReadOnlyList<GitStatusEntry> Entries,
    DateTimeOffset CapturedAt)
{
    /// <summary>
    /// Gets a value indicating whether the repository had no reported changes.
    /// </summary>
    public bool IsClean => Entries.Count == 0;
}
