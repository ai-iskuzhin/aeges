namespace Aeges.Runners.Codex;

/// <summary>
/// Describes a Codex CLI command without launching it.
/// </summary>
/// <param name="Executable">The Codex CLI executable name or path.</param>
/// <param name="Arguments">The command-line arguments.</param>
/// <param name="WorkingDirectory">The working directory used for execution.</param>
/// <param name="Timeout">The maximum execution timeout.</param>
/// <param name="EnvironmentVariables">Environment variables supplied to the process.</param>
public sealed record CodexRunnerCommand(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> EnvironmentVariables);
