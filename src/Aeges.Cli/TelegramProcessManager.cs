using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aeges.Application.Runtime;

internal sealed class TelegramProcessManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly RuntimeDirectoryLayout layout;

    public TelegramProcessManager(RuntimeDirectoryLayout? layout = null)
    {
        this.layout = layout ?? RuntimeDirectoryLayout.CreateDefault();
    }

    public async Task<TelegramProcessStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath();

        if (!File.Exists(metadataPath))
        {
            return TelegramProcessStatus.NotRunning(metadataPath);
        }

        TelegramProcessMetadata? metadata;

        await using (var stream = File.OpenRead(metadataPath))
        {
            metadata = await JsonSerializer.DeserializeAsync<TelegramProcessMetadata>(
                stream,
                JsonOptions,
                cancellationToken);
        }

        if (metadata is null)
        {
            return TelegramProcessStatus.Stale(metadataPath, null);
        }

        return IsProcessRunning(metadata.ProcessId)
            ? TelegramProcessStatus.Running(metadataPath, metadata)
            : TelegramProcessStatus.Stale(metadataPath, metadata);
    }

    public async Task<TelegramProcessStartResult> StartAsync(
        TelegramProcessStartRequest request,
        CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);

        if (status.IsRunning)
        {
            return TelegramProcessStartResult.FromAlreadyRunning(status);
        }

        Directory.CreateDirectory(layout.RunsPath);
        Directory.CreateDirectory(layout.LogsPath);

        if (status.IsStale)
        {
            File.Delete(status.MetadataPath);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var stdoutPath = Path.Combine(layout.LogsPath, "telegram.stdout.log");
        var stderrPath = Path.Combine(layout.LogsPath, "telegram.stderr.log");
        var executablePath = ResolveExecutablePath();
        var arguments = BuildRunArguments(request);
        await AppendLauncherLogAsync(
            stdoutPath,
            $"Starting Telegram transport executable={executablePath} config={request.ConfigPath ?? "(default)"}",
            cancellationToken);
        var processId = await StartDetachedProcessAsync(
            executablePath,
            arguments,
            stdoutPath,
            stderrPath,
            cancellationToken);
        await AppendLauncherLogAsync(
            stdoutPath,
            $"Started Telegram transport pid={processId}",
            cancellationToken);
        var metadata = new TelegramProcessMetadata(
            processId,
            startedAt,
            executablePath,
            arguments,
            stdoutPath,
            stderrPath,
            request.ConfigPath,
            request.ConnectionString);

        await WriteMetadataAsync(metadata, cancellationToken);

        return TelegramProcessStartResult.FromStarted(
            TelegramProcessStatus.Running(GetMetadataPath(), metadata));
    }

    public async Task<TelegramProcessStopResult> StopAsync(CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(cancellationToken);

        if (!status.IsRunning)
        {
            if (File.Exists(status.MetadataPath))
            {
                File.Delete(status.MetadataPath);
            }

            return TelegramProcessStopResult.FromNotRunning(status);
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

        return TelegramProcessStopResult.FromStopped(status);
    }

    private async Task WriteMetadataAsync(
        TelegramProcessMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(GetMetadataPath());
        await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }

    private string GetMetadataPath() =>
        Path.Combine(layout.RunsPath, "telegram.pid.json");

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

    private static IReadOnlyList<string> BuildRunArguments(TelegramProcessStartRequest request)
    {
        var arguments = new List<string>
        {
            "telegram",
            "run",
            "--no-interactive",
            "--poll-limit",
            request.PollLimit.ToString(),
            "--timeout-seconds",
            request.TimeoutSeconds.ToString(),
        };

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
            ?? throw new InvalidOperationException("Failed to start Telegram transport process.");

        return process.Id;
    }

    private async Task<int> StartDetachedUnixProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken)
    {
        var childPidPath = Path.Combine(layout.RunsPath, "telegram.child.pid");
        File.Delete(childPidPath);

        using var launcher = Process.Start(CreateUnixStartInfo(
            executablePath,
            arguments,
            stdoutPath,
            stderrPath,
            childPidPath))
            ?? throw new InvalidOperationException("Failed to start Telegram transport process.");
        await launcher.WaitForExitAsync(cancellationToken);

        if (launcher.ExitCode != 0)
        {
            throw new InvalidOperationException($"Telegram transport launcher exited with code {launcher.ExitCode}.");
        }

        var childPidText = await File.ReadAllTextAsync(childPidPath, cancellationToken);
        File.Delete(childPidPath);

        return int.TryParse(childPidText.Trim(), out var childPid)
            ? childPid
            : throw new InvalidOperationException("Telegram transport launcher did not report a child process ID.");
    }

    private static async Task AppendLauncherLogAsync(
        string path,
        string message,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(
            path,
            $"{DateTimeOffset.UtcNow:O} [telegram-launcher] info: {message}{Environment.NewLine}",
            cancellationToken);
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
        startInfo.ArgumentList.Add(
            $"nohup {QuoteShell(executablePath)} {string.Join(' ', arguments.Select(QuoteShell))} </dev/null >> {QuoteShell(stdoutPath)} 2>> {QuoteShell(stderrPath)} & echo $! > {QuoteShell(childPidPath)}");

        return startInfo;
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

internal sealed record TelegramProcessStartRequest(
    string? ConfigPath,
    string? ConnectionString,
    int PollLimit,
    int TimeoutSeconds);

internal sealed record TelegramProcessMetadata(
    int ProcessId,
    DateTimeOffset StartedAt,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string StdoutPath,
    string StderrPath,
    string? ConfigPath,
    string? ConnectionString);

internal sealed record TelegramProcessStatus(
    string MetadataPath,
    bool IsRunning,
    bool IsStale,
    TelegramProcessMetadata? Metadata)
{
    public static TelegramProcessStatus NotRunning(string metadataPath) =>
        new(metadataPath, false, false, null);

    public static TelegramProcessStatus Running(string metadataPath, TelegramProcessMetadata metadata) =>
        new(metadataPath, true, false, metadata);

    public static TelegramProcessStatus Stale(string metadataPath, TelegramProcessMetadata? metadata) =>
        new(metadataPath, false, true, metadata);
}

internal sealed record TelegramProcessStartResult(
    bool Started,
    bool AlreadyRunning,
    TelegramProcessStatus Status)
{
    public static TelegramProcessStartResult FromStarted(TelegramProcessStatus status) =>
        new(true, false, status);

    public static TelegramProcessStartResult FromAlreadyRunning(TelegramProcessStatus status) =>
        new(false, true, status);
}

internal sealed record TelegramProcessStopResult(
    bool Stopped,
    bool WasRunning,
    TelegramProcessStatus PreviousStatus)
{
    public static TelegramProcessStopResult FromStopped(TelegramProcessStatus previousStatus) =>
        new(true, true, previousStatus);

    public static TelegramProcessStopResult FromNotRunning(TelegramProcessStatus previousStatus) =>
        new(false, false, previousStatus);
}
