using System.Diagnostics;

namespace Aeges.Runners.Codex;

/// <summary>
/// Executes Codex CLI commands as local operating system processes.
/// </summary>
public sealed class ProcessCodexCommandExecutor : ICodexCommandExecutor
{
    /// <inheritdoc />
    public async Task<CodexCommandExecutionResult> ExecuteAsync(
        CodexRunnerCommand command,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken)
    {
        EnsureParentDirectory(stdoutPath);
        EnsureParentDirectory(stderrPath);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(command),
        };

        if (!process.Start())
        {
            return CodexCommandExecutionResult.Exited(-1);
        }

        using var timeout = new CancellationTokenSource(command.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        await using var stdout = new StreamWriter(File.Open(stdoutPath, FileMode.Create, FileAccess.Write, FileShare.Read));
        await using var stderr = new StreamWriter(File.Open(stderrPath, FileMode.Create, FileAccess.Write, FileShare.Read));
        var stdoutTask = CopyOutputAsync(process.StandardOutput, stdout, CancellationToken.None);
        var stderrTask = CopyOutputAsync(process.StandardError, stderr, CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token);
            await Task.WhenAll(stdoutTask, stderrTask);

            return CodexCommandExecutionResult.Exited(process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            await Task.WhenAll(stdoutTask, stderrTask);

            return cancellationToken.IsCancellationRequested
                ? CodexCommandExecutionResult.Cancellation("Codex runner was cancelled.")
                : CodexCommandExecutionResult.Timeout("Codex runner exceeded its timeout.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(CodexRunnerCommand command)
    {
        var startInfo = new ProcessStartInfo(command.Executable)
        {
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in command.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        return startInfo;
    }

    private static async Task CopyOutputAsync(
        TextReader reader,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            await writer.WriteLineAsync(line);
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void KillProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
    }
}
