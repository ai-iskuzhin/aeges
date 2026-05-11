using Aeges.Core;
using Aeges.Runners;
using System.Text.Json;

namespace Aeges.Runners.Codex;

/// <summary>
/// Executes governed discussion turns through the Codex CLI.
/// </summary>
public sealed class CodexTalkRunner : ITalkRunner
{
    private readonly CodexRunnerOptions options;
    private readonly CodexRunnerCommandBuilder availability;
    private readonly ICodexCommandExecutor commandExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexTalkRunner"/> class.
    /// </summary>
    /// <param name="options">The Codex runner options.</param>
    /// <param name="commandExecutor">The command executor used to launch Codex.</param>
    /// <param name="executableResolver">The resolver used to locate the Codex executable.</param>
    public CodexTalkRunner(
        CodexRunnerOptions? options = null,
        ICodexCommandExecutor? commandExecutor = null,
        ICodexExecutableResolver? executableResolver = null)
    {
        this.options = options ?? new CodexRunnerOptions(SandboxMode: "read-only");
        availability = new CodexRunnerCommandBuilder(this.options, executableResolver);
        this.commandExecutor = commandExecutor ?? new ProcessCodexCommandExecutor();
    }

    /// <inheritdoc />
    public RunnerId Id { get; } = new("codex");

    /// <inheritdoc />
    public async Task<TalkRunnerResult> SendAsync(
        TalkRunnerRequest request,
        CancellationToken cancellationToken)
    {
        var stdoutPath = Path.Combine(request.ArtifactOutputDirectory, "codex-talk.stdout.jsonl");
        var stderrPath = Path.Combine(request.ArtifactOutputDirectory, "codex-talk.stderr.log");
        var responsePath = Path.Combine(request.ArtifactOutputDirectory, "response.md");

        var resolved = availability.CheckAvailability();

        if (!resolved.IsAvailable)
        {
            return TalkRunnerResult.Failed(resolved.Message);
        }

        var command = BuildCommand(request);

        try
        {
            var execution = await commandExecutor.ExecuteAsync(command, stdoutPath, stderrPath, cancellationToken);
            var externalSessionId = TryReadThreadId(stdoutPath) ?? request.ExternalSessionId;

            if (execution.ExitCode != 0)
            {
                return TalkRunnerResult.Failed(
                    execution.ErrorSummary ?? $"Codex exited with code {execution.ExitCode}.",
                    execution.ExitCode,
                    stdoutPath,
                    stderrPath,
                    externalSessionId);
            }

            var response = TryReadLatestAgentMessage(stdoutPath)
                ?? "Codex completed without a readable response.";
            await File.WriteAllTextAsync(responsePath, response, cancellationToken);

            return TalkRunnerResult.Succeeded(
                response,
                execution.ExitCode ?? 0,
                stdoutPath,
                stderrPath,
                responsePath,
                externalSessionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TalkRunnerResult.Failed("Codex talk runner was cancelled.", stdoutPath: stdoutPath, stderrPath: stderrPath);
        }
    }

    private CodexRunnerCommand BuildCommand(TalkRunnerRequest request)
    {
        var arguments = new List<string>();
        arguments.AddRange(options.BaseArguments ?? CreateDefaultBaseArguments(request.WorkingDirectory, options));

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

        arguments.Add("-");

        var environment = new Dictionary<string, string>(request.EnvironmentVariables);

        foreach (var pair in options.EnvironmentVariables ?? new Dictionary<string, string>())
        {
            environment[pair.Key] = pair.Value;
        }

        environment["AEGES_TALK_SESSION_ID"] = request.SessionId.Value;
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
            request.WorkingDirectory,
            request.Timeout,
            environment,
            request.Prompt);
    }

    private static IReadOnlyList<string> CreateDefaultBaseArguments(
        string workingDirectory,
        CodexRunnerOptions options)
    {
        if (options.BypassApprovalsAndSandbox)
        {
            return
            [
                "--dangerously-bypass-approvals-and-sandbox",
                "exec",
                "--json",
                "--cd",
                workingDirectory,
            ];
        }

        return
        [
            "--ask-for-approval",
            "never",
            "exec",
            "--json",
            "--sandbox",
            options.SandboxMode,
            "--cd",
            workingDirectory,
        ];
    }

    private static string? TryReadThreadId(string stdoutPath)
    {
        if (!File.Exists(stdoutPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(stdoutPath))
        {
            if (CodexRunnerJsonEvents.TryReadThreadId(line, out var threadId))
            {
                return threadId;
            }
        }

        return null;
    }

    private static string? TryReadLatestAgentMessage(string stdoutPath)
    {
        if (!File.Exists(stdoutPath))
        {
            return null;
        }

        string? latestMessage = null;

        foreach (var line in File.ReadLines(stdoutPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (!root.TryGetProperty("type", out var type)
                    || type.GetString() != "item.completed"
                    || !root.TryGetProperty("item", out var item)
                    || !item.TryGetProperty("type", out var itemType)
                    || itemType.GetString() != "agent_message"
                    || !item.TryGetProperty("text", out var text))
                {
                    continue;
                }

                latestMessage = text.GetString();
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return string.IsNullOrWhiteSpace(latestMessage) ? null : latestMessage.Trim();
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
}
