namespace Aeges.Runners.Codex;

/// <summary>
/// Resolves a configured Codex executable name or path on the current machine.
/// </summary>
public interface ICodexExecutableResolver
{
    /// <summary>
    /// Resolves the configured executable.
    /// </summary>
    /// <param name="executable">The executable name or path.</param>
    /// <returns>The resolved executable path, or <see langword="null"/> when none is found.</returns>
    string? Resolve(string executable);
}
