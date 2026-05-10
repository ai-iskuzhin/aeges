using Aeges.Runners;

namespace Aeges.Runners.Codex;

/// <summary>
/// Builds Codex CLI command descriptions from governed runner requests.
/// </summary>
public sealed class CodexRunnerCommandBuilder
{
    private readonly CodexRunnerOptions options;
    private readonly ICodexExecutableResolver executableResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexRunnerCommandBuilder"/> class.
    /// </summary>
    /// <param name="options">The Codex runner options.</param>
    /// <param name="executableResolver">The resolver used to locate the Codex CLI executable.</param>
    public CodexRunnerCommandBuilder(
        CodexRunnerOptions? options = null,
        ICodexExecutableResolver? executableResolver = null)
    {
        this.options = Validate(options ?? new CodexRunnerOptions());
        this.executableResolver = executableResolver ?? new PathCodexExecutableResolver();
    }

    /// <summary>
    /// Checks whether the configured Codex CLI executable exists on this machine.
    /// </summary>
    /// <returns>The Codex runner availability result.</returns>
    public CodexRunnerAvailability CheckAvailability()
    {
        var resolvedPath = executableResolver.Resolve(options.Executable);

        return resolvedPath is null
            ? CodexRunnerAvailability.Missing(options.Executable)
            : CodexRunnerAvailability.Available(options.Executable, resolvedPath);
    }

    /// <summary>
    /// Builds a Codex CLI command description without launching Codex.
    /// </summary>
    /// <param name="request">The governed runner request.</param>
    /// <returns>The command description.</returns>
    public CodexRunnerCommand Build(RunnerRequest request)
    {
        var availability = CheckAvailability();

        if (!availability.IsAvailable)
        {
            throw new CodexRunnerUnavailableException(availability);
        }

        var arguments = new List<string>();
        arguments.AddRange(options.BaseArguments ?? CreateDefaultBaseArguments(request.WorktreePath));

        if (request.SessionPolicy == RunnerSessionPolicy.ResumeSession)
        {
            arguments.Add("resume");
        }

        if (options.Model is not null)
        {
            arguments.Add("--model");
            arguments.Add(options.Model);
        }

        if (options.ReasoningEffort is not null)
        {
            arguments.Add("--config");
            arguments.Add($"model_reasoning_effort={ToTomlStringLiteral(options.ReasoningEffort)}");
        }

        if (request.SessionPolicy == RunnerSessionPolicy.ResumeSession)
        {
            arguments.Add(request.ExternalSessionId!);
        }

        var promptText = File.ReadAllText(request.PromptPath);
        arguments.Add("-");

        var environment = new Dictionary<string, string>(request.EnvironmentVariables);

        foreach (var pair in options.EnvironmentVariables ?? new Dictionary<string, string>())
        {
            environment[pair.Key] = pair.Value;
        }

        environment["AEGES_TASK_ID"] = request.TaskId.Value;
        environment["AEGES_ITERATION_ID"] = request.IterationId.Value;
        environment["AEGES_PROJECT_ID"] = request.ProjectId.Value;
        environment["AEGES_ARTIFACT_OUTPUT_DIRECTORY"] = request.ArtifactOutputDirectory;
        environment["AEGES_RUNNER_SESSION_POLICY"] = request.SessionPolicy.ToString();

        if (request.ExternalSessionId is not null)
        {
            environment["AEGES_EXTERNAL_SESSION_ID"] = request.ExternalSessionId;
        }

        foreach (var policyHint in request.PolicyHints)
        {
            environment[$"AEGES_POLICY_{NormalizeEnvironmentKey(policyHint.Key)}"] = policyHint.Value;
        }

        return new CodexRunnerCommand(
            options.Executable,
            arguments,
            request.WorktreePath,
            request.Timeout,
            environment,
            promptText);
    }

    private static CodexRunnerOptions Validate(CodexRunnerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Executable))
        {
            throw new ArgumentException("Executable must not be empty.", nameof(options));
        }

        if (options.BaseArguments?.Any(string.IsNullOrWhiteSpace) == true)
        {
            throw new ArgumentException("Base arguments must not be empty.", nameof(options));
        }

        if (options.Model is not null && string.IsNullOrWhiteSpace(options.Model))
        {
            throw new ArgumentException("Model must not be empty when configured.", nameof(options));
        }

        if (options.ReasoningEffort is not null && string.IsNullOrWhiteSpace(options.ReasoningEffort))
        {
            throw new ArgumentException("Reasoning effort must not be empty when configured.", nameof(options));
        }

        if (options.EnvironmentVariables?.Any(pair => string.IsNullOrWhiteSpace(pair.Key)) == true)
        {
            throw new ArgumentException("Environment variable names must not be empty.", nameof(options));
        }

        return options;
    }

    private static string NormalizeEnvironmentKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Policy hint key must not be empty.", nameof(key));
        }

        return string.Concat(
            key.Select(character => char.IsAsciiLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : '_'));
    }

    private static string ToTomlStringLiteral(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static IReadOnlyList<string> CreateDefaultBaseArguments(string worktreePath) =>
    [
        "--ask-for-approval",
        "never",
        "exec",
        "--json",
        "--sandbox",
        "workspace-write",
        "--cd",
        worktreePath,
    ];
}
