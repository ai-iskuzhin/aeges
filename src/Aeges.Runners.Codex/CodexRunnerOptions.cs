namespace Aeges.Runners.Codex;

/// <summary>
/// Configures Codex CLI command construction.
/// </summary>
/// <param name="Executable">The Codex CLI executable name or path.</param>
/// <param name="BaseArguments">Arguments placed before the prompt path.</param>
/// <param name="EnvironmentVariables">Environment variables supplied to Codex in addition to runner request variables.</param>
public sealed record CodexRunnerOptions(
    string Executable = "codex",
    IReadOnlyList<string>? BaseArguments = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);
