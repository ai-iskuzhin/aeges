namespace Aeges.Runners.Codex;

/// <summary>
/// Configures Codex CLI command construction.
/// </summary>
/// <param name="Executable">The Codex CLI executable name or path.</param>
/// <param name="BaseArguments">Arguments placed before the stdin prompt marker.</param>
/// <param name="Model">The Codex model identifier to pass with <c>--model</c>, when configured.</param>
/// <param name="ReasoningEffort">The Codex model reasoning effort to pass through configuration, when configured.</param>
/// <param name="EnvironmentVariables">Environment variables supplied to Codex in addition to runner request variables.</param>
public sealed record CodexRunnerOptions(
    string Executable = "codex",
    IReadOnlyList<string>? BaseArguments = null,
    string? Model = null,
    string? ReasoningEffort = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);
