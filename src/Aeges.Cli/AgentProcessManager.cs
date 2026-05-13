using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aeges.Application.Runtime;

internal sealed class AgentProcessManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly RuntimeDirectoryLayout layout;

    public AgentProcessManager(RuntimeDirectoryLayout? layout = null)
    {
        this.layout = layout ?? RuntimeDirectoryLayout.CreateDefault();
    }

    public async Task<AgentProcessStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath();

        if (!File.Exists(metadataPath))
        {
            return AgentProcessStatus.NotRunning(metadataPath);
        }

        AgentProcessMetadata? metadata;

        await using (var stream = File.OpenRead(metadataPath))
        {
            metadata = await JsonSerializer.DeserializeAsync<AgentProcessMetadata>(
                stream,
                JsonOptions,
                cancellationToken);
        }

        if (metadata is null)
        {
            return AgentProcessStatus.Stale(metadataPath, null);
        }

        return IsProcessRunning(metadata.ProcessId)
            ? AgentProcessStatus.Running(metadataPath, metadata)
            : AgentProcessStatus.Stale(metadataPath, metadata);
    }

    public async Task<AgentProcessStartResult> StartAsync(
        AgentProcessStartRequest request,
        CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);

        if (status.IsRunning)
        {
            return AgentProcessStartResult.FromAlreadyRunning(status);
        }

        Directory.CreateDirectory(layout.RunsPath);
        Directory.CreateDirectory(layout.LogsPath);

        if (status.IsStale)
        {
            File.Delete(status.MetadataPath);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var stdoutPath = Path.Combine(layout.LogsPath, "agent.stdout.log");
        var stderrPath = Path.Combine(layout.LogsPath, "agent.stderr.log");
        var executablePath = ResolveExecutablePath();
        var arguments = BuildRunArguments(request);
        PrepareProcessLogs(stdoutPath, stderrPath);
        await AppendLauncherLogAsync(
            stdoutPath,
            $"Starting agent worker executable={executablePath} config={request.ConfigPath ?? "(default)"}",
            cancellationToken);
        var processId = await StartDetachedProcessAsync(
            executablePath,
            arguments,
            stdoutPath,
            stderrPath,
            cancellationToken);
        await AppendLauncherLogAsync(
            stdoutPath,
            $"Started agent worker pid={processId}",
            cancellationToken);
        var metadata = new AgentProcessMetadata(
            processId,
            startedAt,
            executablePath,
            arguments,
            stdoutPath,
            stderrPath,
            request.ConfigPath,
            request.ConnectionString);

        await WriteMetadataAsync(metadata, cancellationToken);

        return AgentProcessStartResult.FromStarted(
            AgentProcessStatus.Running(GetMetadataPath(), metadata));
    }

    public async Task<AgentProcessStopResult> StopAsync(CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);

        if (!status.IsRunning)
        {
            if (File.Exists(status.MetadataPath))
            {
                File.Delete(status.MetadataPath);
            }

            return AgentProcessStopResult.FromNotRunning(status);
        }

        try
        {
            var process = Process.GetProcessById(status.Metadata!.ProcessId);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            if (File.Exists(status.MetadataPath))
            {
                File.Delete(status.MetadataPath);
            }
        }

        return AgentProcessStopResult.FromStopped(status);
    }

    private async Task WriteMetadataAsync(
        AgentProcessMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(GetMetadataPath());
        await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }

    private string GetMetadataPath() =>
        Path.Combine(layout.RunsPath, "agent.pid.json");

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string ResolveExecutablePath() =>
        ResolveFromPath("aeges") ?? Environment.ProcessPath ?? "aeges";

    private static string? ResolveFromPath(string executable)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executable);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> BuildRunArguments(AgentProcessStartRequest request)
    {
        var arguments = new List<string>
        {
            "agent",
            "run",
            "--poll-interval-seconds",
            request.PollIntervalSeconds.ToString(),
            "--queue-preview-limit",
            request.QueuePreviewLimit.ToString(),
            "--max-parallel-tasks",
            request.MaxParallelTasks.ToString(),
        };

        if (!request.ClaimQueuedTask)
        {
            arguments.Add("--no-claim");
        }

        if (request.CreateWorktree)
        {
            arguments.Add("--create-worktree");
        }

        if (request.ExecuteRunner)
        {
            arguments.Add("--execute-runner");
        }

        AddOptional(arguments, "--machine-id", request.MachineId);
        AddOptional(arguments, "--machine-name", request.MachineName);
        AddOptional(arguments, "--platform", request.Platform);
        AddOptional(arguments, "--runner-id", request.RunnerId);

        if (request.ConfigPath is not null)
        {
            arguments.Add("--config");
            arguments.Add(request.ConfigPath);
        }

        if (request.ConnectionString is not null)
        {
            arguments.Add("--connection-string");
            arguments.Add(request.ConnectionString);
        }

        return arguments;
    }

    private static void AddOptional(List<string> arguments, string optionName, string? value)
    {
        if (value is null)
        {
            return;
        }

        arguments.Add(optionName);
        arguments.Add(value);
    }

    private async Task<int> StartDetachedProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await StartDetachedUnixProcessAsync(
                executablePath,
                arguments,
                stdoutPath,
                stderrPath,
                cancellationToken);
        }

        using var process = Process.Start(CreateWindowsStartInfo(executablePath, arguments, stdoutPath, stderrPath))
            ?? throw new InvalidOperationException("Failed to start agent worker process.");

        return process.Id;
    }

    private async Task<int> StartDetachedUnixProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken)
    {
        var childPidPath = Path.Combine(layout.RunsPath, "agent.child.pid");
        File.Delete(childPidPath);

        using var launcher = Process.Start(CreateUnixStartInfo(
            executablePath,
            arguments,
            stdoutPath,
            stderrPath,
            childPidPath))
            ?? throw new InvalidOperationException("Failed to start agent worker process.");
        await launcher.WaitForExitAsync(cancellationToken);

        if (launcher.ExitCode != 0)
        {
            throw new InvalidOperationException($"Agent worker launcher exited with code {launcher.ExitCode}.");
        }

        var childPidText = await File.ReadAllTextAsync(childPidPath, cancellationToken);
        File.Delete(childPidPath);

        return int.TryParse(childPidText.Trim(), out var childPid)
            ? childPid
            : throw new InvalidOperationException("Agent worker launcher did not report a child process ID.");
    }

    private static async Task AppendLauncherLogAsync(
        string path,
        string message,
        CancellationToken cancellationToken)
    {
        await RuntimeLogFiles.AppendLineAsync(
            path,
            $"{DateTimeOffset.UtcNow:O} [agent-launcher] info: {message}",
            cancellationToken);
    }

    private static void PrepareProcessLogs(string stdoutPath, string stderrPath)
    {
        RuntimeLogFiles.RotateIfNeeded(
            stdoutPath,
            RuntimeLogFiles.DefaultMaxBytes,
            RuntimeLogFiles.DefaultRetainedFiles);
        RuntimeLogFiles.RotateIfNeeded(
            stderrPath,
            RuntimeLogFiles.DefaultMaxBytes,
            RuntimeLogFiles.DefaultRetainedFiles);
    }

    private static ProcessStartInfo CreateUnixStartInfo(
        string executablePath,
        IReadOnlyList<string> arguments,
        string stdoutPath,
        string stderrPath,
        string childPidPath)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        startInfo.FileName = "/bin/sh";
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(BuildUnixDetachedCommand(
            executablePath,
            arguments,
            stdoutPath,
            stderrPath,
            childPidPath));

        return startInfo;
    }

    private static string BuildUnixDetachedCommand(
        string executablePath,
        IReadOnlyList<string> arguments,
        string stdoutPath,
        string stderrPath,
        string childPidPath)
    {
        var executableAndArguments = $"{QuoteShell(executablePath)} {string.Join(' ', arguments.Select(QuoteShell))}";
        var perlSessionScript = "setsid() or die \"setsid: $!\"; exec @ARGV or die \"exec: $!\"";

        return
            "(if command -v setsid >/dev/null 2>&1; then " +
            $"exec setsid {executableAndArguments}; " +
            "elif command -v perl >/dev/null 2>&1; then " +
            $"exec perl -MPOSIX=setsid -e {QuoteShell(perlSessionScript)} {executableAndArguments}; " +
            "else " +
            $"exec nohup {executableAndArguments}; " +
            "fi) " +
            $"</dev/null >> {QuoteShell(stdoutPath)} 2>> {QuoteShell(stderrPath)} & echo $! > {QuoteShell(childPidPath)}";
    }

    private static ProcessStartInfo CreateWindowsStartInfo(
        string executablePath,
        IReadOnlyList<string> arguments,
        string stdoutPath,
        string stderrPath)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
        };

        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(
            $"{QuoteWindows(executablePath)} {string.Join(' ', arguments.Select(QuoteWindows))} >> {QuoteWindows(stdoutPath)} 2>> {QuoteWindows(stderrPath)}");

        return startInfo;
    }

    private static string QuoteShell(string value) =>
        "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

    private static string QuoteWindows(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

internal sealed record AgentProcessStartRequest(
    string? ConfigPath,
    string? ConnectionString,
    string? MachineId,
    string? MachineName,
    string? Platform,
    string? RunnerId,
    int PollIntervalSeconds,
    int QueuePreviewLimit,
    int MaxParallelTasks,
    bool ClaimQueuedTask,
    bool ExecuteRunner,
    bool CreateWorktree);

internal sealed record AgentProcessMetadata(
    int ProcessId,
    DateTimeOffset StartedAt,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string StdoutPath,
    string StderrPath,
    string? ConfigPath,
    string? ConnectionString);

internal sealed record AgentProcessStatus(
    string MetadataPath,
    bool IsRunning,
    bool IsStale,
    AgentProcessMetadata? Metadata)
{
    public static AgentProcessStatus NotRunning(string metadataPath) =>
        new(metadataPath, false, false, null);

    public static AgentProcessStatus Running(string metadataPath, AgentProcessMetadata metadata) =>
        new(metadataPath, true, false, metadata);

    public static AgentProcessStatus Stale(string metadataPath, AgentProcessMetadata? metadata) =>
        new(metadataPath, false, true, metadata);
}

internal sealed record AgentProcessStartResult(
    bool Started,
    bool AlreadyRunning,
    AgentProcessStatus Status)
{
    public static AgentProcessStartResult FromStarted(AgentProcessStatus status) =>
        new(true, false, status);

    public static AgentProcessStartResult FromAlreadyRunning(AgentProcessStatus status) =>
        new(false, true, status);
}

internal sealed record AgentProcessStopResult(
    bool Stopped,
    bool WasRunning,
    AgentProcessStatus PreviousStatus)
{
    public static AgentProcessStopResult FromStopped(AgentProcessStatus previousStatus) =>
        new(true, true, previousStatus);

    public static AgentProcessStopResult FromNotRunning(AgentProcessStatus previousStatus) =>
        new(false, false, previousStatus);
}
