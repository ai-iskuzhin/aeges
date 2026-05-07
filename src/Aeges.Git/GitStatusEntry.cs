namespace Aeges.Git;

/// <summary>
/// Describes one path reported by Git status.
/// </summary>
/// <param name="Path">The repository-relative path.</param>
/// <param name="StatusCode">The stable Git porcelain status code.</param>
public sealed record GitStatusEntry(string Path, string StatusCode);
