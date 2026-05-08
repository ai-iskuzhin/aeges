namespace Aeges.Runners.Codex;

/// <summary>
/// Describes whether the configured Codex CLI executable is available on the current machine.
/// </summary>
/// <param name="IsAvailable">Whether the executable was found.</param>
/// <param name="Executable">The configured executable name or path.</param>
/// <param name="ResolvedPath">The resolved executable path, when found.</param>
/// <param name="Message">The user-facing availability message.</param>
public sealed record CodexRunnerAvailability(
    bool IsAvailable,
    string Executable,
    string? ResolvedPath,
    string Message)
{
    /// <summary>
    /// Gets the Codex project URL shown when installation is required.
    /// </summary>
    public const string CodexProjectUrl = "https://github.com/openai/codex";

    /// <summary>
    /// Creates an available result.
    /// </summary>
    /// <param name="executable">The configured executable name or path.</param>
    /// <param name="resolvedPath">The resolved executable path.</param>
    /// <returns>An available Codex runner result.</returns>
    public static CodexRunnerAvailability Available(string executable, string resolvedPath) =>
        new(true, executable, resolvedPath, $"Codex CLI executable '{executable}' was found at '{resolvedPath}'.");

    /// <summary>
    /// Creates a missing result with an installation URL for the user.
    /// </summary>
    /// <param name="executable">The configured executable name or path.</param>
    /// <returns>A missing Codex runner result.</returns>
    public static CodexRunnerAvailability Missing(string executable) =>
        new(
            false,
            executable,
            ResolvedPath: null,
            $"Codex CLI executable '{executable}' was not found on this machine. "
            + $"Install Codex or configure the executable path. Project: {CodexProjectUrl}");
}
