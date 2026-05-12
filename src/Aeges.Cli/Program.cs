using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aeges.Agent;
using Aeges.Application;
using Aeges.Application.Approvals;
using Aeges.Application.Artifacts;
using Aeges.Application.Configuration;
using Aeges.Application.Iterations;
using Aeges.Application.Machines;
using Aeges.Application.Projects;
using Aeges.Application.RunnerExecutions;
using Aeges.Application.Runtime;
using Aeges.Application.Talk;
using Aeges.Application.TelegramTaskBindings;
using Aeges.Application.TelegramUsers;
using Aeges.Application.Tasks;
using Aeges.Application.Transports;
using Aeges.Core;
using Aeges.Runners.Codex;
using Aeges.Storage;
using Aeges.Storage.Sqlite;
using Aeges.Telegram;
using Microsoft.EntityFrameworkCore;

using var cancellation = new CancellationTokenSource();

// Keep the CLI interruptible without letting Ctrl+C terminate in the middle of
// a storage update or agent heartbeat.
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await AegesCli.RunAsync(args, Console.In, Console.Out, Console.Error, cancellation.Token);

internal static class AegesCli
{
    private static readonly AsyncLocal<CliTrace?> CurrentTrace = new();

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        await RunAsync(args, Console.In, output, error, cancellationToken);

    public static async Task<int> RunAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args is ["-v"])
        {
            return await RunCoreAsync(args, input, output, error, cancellationToken);
        }

        var traceOptions = CliTraceOptions.Parse(args);
        var trace = new CliTrace(traceOptions.Verbose, error);
        var previousTrace = CurrentTrace.Value;
        CurrentTrace.Value = trace;
        var stopwatch = Stopwatch.StartNew();

        await trace.WriteAsync($"command: {FormatTraceCommand(traceOptions.Args)}");
        await trace.WriteAsync($"version: {GetVersion()}");
        await trace.WriteAsync($"cwd: {Environment.CurrentDirectory}");

        try
        {
            var exitCode = await RunCoreAsync(traceOptions.Args, input, output, error, cancellationToken);
            await trace.WriteAsync($"exit-code: {exitCode}");
            await trace.WriteAsync($"elapsed-ms: {stopwatch.ElapsedMilliseconds}");

            return exitCode;
        }
        catch (Exception exception)
        {
            await trace.WriteAsync($"exception: {exception.GetType().Name}: {exception.Message}");
            await trace.WriteAsync($"elapsed-ms: {stopwatch.ElapsedMilliseconds}");
            throw;
        }
        finally
        {
            CurrentTrace.Value = previousTrace;
        }
    }

    private static async Task<int> RunCoreAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args is ["init", .. var initArgs])
        {
            return await RunInitAsync(initArgs, output, error, cancellationToken);
        }

        if (args is ["setup", .. var setupArgs])
        {
            return await RunSetupAsync(setupArgs, input, output, error, cancellationToken);
        }

        if (args is ["version", .. var versionArgs])
        {
            return await RunVersionAsync(versionArgs, output, error);
        }

        if (args is ["--version", .. var versionArgsAlias])
        {
            return await RunVersionAsync(versionArgsAlias, output, error);
        }

        if (args is ["-v", .. var versionArgsShort])
        {
            return await RunVersionAsync(versionArgsShort, output, error);
        }

        if (args is ["update", .. var updateArgs])
        {
            return await RunUpdateAsync(updateArgs, output, error, cancellationToken);
        }

        if (args is ["status", .. var localStatusArgs])
        {
            return await RunLocalStatusAsync(localStatusArgs, output, error, cancellationToken);
        }

        if (args is ["talk", .. var talkArgs])
        {
            return await RunTalkAsync(talkArgs, input, output, error, cancellationToken);
        }

        if (args is ["db", "status", .. var statusArgs])
        {
            return await RunDatabaseStatusAsync(statusArgs, output, error, cancellationToken);
        }

        if (args is ["db", "migrate", .. var migrateArgs])
        {
            return await RunDatabaseMigrateAsync(migrateArgs, output, error, cancellationToken);
        }

        if (args is ["task", "create", .. var createArgs])
        {
            return await RunTaskCreateAsync(createArgs, output, error, cancellationToken);
        }

        if (args is ["project", "add", .. var projectAddArgs])
        {
            return await RunProjectAddAsync(projectAddArgs, output, error, cancellationToken);
        }

        if (args is ["project", "list", .. var projectListArgs])
        {
            return await RunProjectListAsync(projectListArgs, output, error, cancellationToken);
        }

        if (args is ["group", "add", .. var groupAddArgs])
        {
            return await RunProjectGroupAddAsync(groupAddArgs, output, error, cancellationToken);
        }

        if (args is ["group", "list", .. var groupListArgs])
        {
            return await RunProjectGroupListAsync(groupListArgs, output, error, cancellationToken);
        }

        if (args is ["root", "add", .. var rootAddArgs])
        {
            return await RunProjectRootAddAsync(rootAddArgs, output, error, cancellationToken);
        }

        if (args is ["root", "list", .. var rootListArgs])
        {
            return await RunProjectRootListAsync(rootListArgs, output, error, cancellationToken);
        }

        if (args is ["root", "scan", .. var rootScanArgs])
        {
            return await RunProjectRootScanAsync(rootScanArgs, output, error, cancellationToken);
        }

        if (args is ["machine", "add", .. var machineAddArgs])
        {
            return await RunMachineAddAsync(machineAddArgs, output, error, cancellationToken);
        }

        if (args is ["machine", "list", .. var machineListArgs])
        {
            return await RunMachineListAsync(machineListArgs, output, error, cancellationToken);
        }

        if (args is ["task", "status", .. var taskStatusArgs])
        {
            return await RunTaskStatusAsync(taskStatusArgs, output, error, cancellationToken);
        }

        if (args is ["task", "cancel", .. var taskCancelArgs])
        {
            return await RunTaskCancelAsync(taskCancelArgs, output, error, cancellationToken);
        }

        if (args is ["task", "continue", .. var taskContinueArgs])
        {
            return await RunTaskContinueAsync(taskContinueArgs, output, error, cancellationToken);
        }

        if (args is ["agent", "run", .. var agentRunArgs])
        {
            return await RunAgentRunAsync(agentRunArgs, output, error, cancellationToken);
        }

        if (args is ["agent", "start", .. var agentStartArgs])
        {
            return await RunAgentStartAsync(agentStartArgs, output, error, cancellationToken);
        }

        if (args is ["agent", "restart", .. var agentRestartArgs])
        {
            return await RunAgentRestartAsync(agentRestartArgs, output, error, cancellationToken);
        }

        if (args is ["agent", "status", .. var agentStatusArgs])
        {
            return await RunAgentStatusAsync(agentStatusArgs, output, error, cancellationToken);
        }

        if (args is ["agent", "stop", .. var agentStopArgs])
        {
            return await RunAgentStopAsync(agentStopArgs, output, error, cancellationToken);
        }

        if (args is ["telegram", "run", .. var telegramRunArgs])
        {
            return await RunTelegramRunAsync(telegramRunArgs, input, output, error, cancellationToken);
        }

        if (args is ["telegram", "start", .. var telegramStartArgs])
        {
            return await RunTelegramStartAsync(telegramStartArgs, input, output, error, cancellationToken);
        }

        if (args is ["telegram", "restart", .. var telegramRestartArgs])
        {
            return await RunTelegramRestartAsync(telegramRestartArgs, input, output, error, cancellationToken);
        }

        if (args is ["telegram", "status", .. var telegramStatusArgs])
        {
            return await RunTelegramStatusAsync(telegramStatusArgs, output, error, cancellationToken);
        }

        if (args is ["telegram", "stop", .. var telegramStopArgs])
        {
            return await RunTelegramStopAsync(telegramStopArgs, output, error, cancellationToken);
        }

        if (args is ["telegram", "setup", .. var telegramSetupArgs])
        {
            return await RunTelegramSetupAsync(telegramSetupArgs, input, output, error, cancellationToken);
        }

        if (args is ["telegram", "check", .. var telegramCheckArgs])
        {
            return await RunTelegramCheckAsync(telegramCheckArgs, output, error, cancellationToken);
        }

        await WriteUsageAsync(error);

        return 2;
    }

    private static async Task<int> RunInitAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = InitOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var result = await InitializeRuntimeAsync(options, cancellationToken);
        await WriteInitResultAsync(result, options.Json, output);

        return 0;
    }

    private static async Task<int> RunVersionAsync(
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await WriteVersionAsync(
            new VersionOutput("aeges", GetVersion()),
            options.Json,
            output);

        return 0;
    }

    private static async Task<int> RunUpdateAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = UpdateOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        try
        {
            if (!options.Json)
            {
                await output.WriteLineAsync("Resolving Aeges update...");
                await output.WriteLineAsync($"Current version: {GetComparableVersion(GetVersion())}");
                await output.FlushAsync(cancellationToken);
            }

            await TraceAsync("update: resolving plan");
            var plan = await CreateUpdatePlanAsync(options, cancellationToken);
            await TraceAsync($"update: target={plan.TargetVersion ?? "latest"} source={plan.PackageSource}");
            if (!string.IsNullOrWhiteSpace(plan.DownloadedPackagePath))
            {
                await TraceAsync($"update: downloaded-package={plan.DownloadedPackagePath}");
            }

            var currentVersion = GetVersion();

            if (options.DryRun)
            {
                await WriteUpdateResultAsync(
                    AegesUpdateResult.FromDryRun(currentVersion, plan.TargetVersion, plan.PackageSource, plan.ToolPackage),
                    options.Json,
                    output);
                return 0;
            }

            if (IsCurrentVersion(plan.TargetVersion, currentVersion))
            {
                await TraceAsync("update: target matches current version");
                await WriteUpdateResultAsync(
                    AegesUpdateResult.FromUpToDate(currentVersion, plan.TargetVersion, plan.PackageSource, plan.ToolPackage),
                    options.Json,
                    output);
                return 0;
            }

            await WriteUpdateProgressAsync(plan, output);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !options.Direct)
            {
                await TraceAsync(options.Elevated
                    ? "update: scheduling elevated Windows deferred updater"
                    : "update: scheduling Windows deferred updater");
                var deferred = await ScheduleWindowsDeferredUpdateAsync(
                    plan,
                    options.Elevated,
                    cancellationToken);

                await WriteUpdateResultAsync(
                    AegesUpdateResult.FromDeferred(
                        currentVersion,
                        plan.TargetVersion,
                        plan.PackageSource,
                        plan.ToolPackage,
                        deferred.ScriptPath,
                        deferred.LogPath,
                        deferred.Elevated),
                    options.Json,
                    output);

                return 0;
            }

            await TraceAsync("update: running dotnet tool update");
            var update = await RunDotnetToolAsync("update", plan, cancellationToken);
            await TraceAsync($"update: dotnet tool update exit={update.ExitCode}");
            var install = update.ExitCode == 0
                ? null
                : await RunDotnetToolAsync("install", plan, cancellationToken);
            if (install is not null)
            {
                await TraceAsync($"update: dotnet tool install exit={install.ExitCode}");
            }
            var effective = update.ExitCode == 0 ? update : install!;
            var result = new AegesUpdateResult(
                update.ExitCode == 0 || install?.ExitCode == 0,
                options.DryRun,
                false,
                false,
                false,
                currentVersion,
                plan.TargetVersion,
                plan.PackageSource,
                plan.ToolPackage,
                null,
                null,
                update.Command,
                update.ExitCode,
                install?.Command,
                install?.ExitCode,
                update.StandardOutput,
                update.StandardError,
                install?.StandardOutput,
                install?.StandardError);

            await WriteUpdateResultAsync(result, options.Json, result.Updated ? output : error);

            return effective.ExitCode == 0 ? 0 : effective.ExitCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            await error.WriteLineAsync($"Aeges update failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunSetupAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = SetupOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await output.WriteLineAsync("Aeges setup");
        await output.WriteLineAsync();

        var initResult = await InitializeRuntimeAsync(options, cancellationToken);
        await WriteInitResultAsync(initResult, json: false, output);
        await output.WriteLineAsync();

        var configPath = ResolveConfigPath(options);
        await TraceAsync("telegram run: loading configuration");
        var configuration = LoadConfiguration(options);
        var telegramConfigured = TelegramCliSetup.HasConfiguredToken(configuration.Telegram);

        if (!options.SkipTelegram)
        {
            var setupTelegram = telegramConfigured
                ? await PromptYesNoAsync(
                    input,
                    output,
                    "Telegram is already configured. Re-run Telegram setup",
                    defaultValue: false,
                    cancellationToken)
                : await PromptYesNoAsync(
                    input,
                    output,
                    "Set up Telegram now",
                    defaultValue: true,
                    cancellationToken);

            if (setupTelegram)
            {
                await TelegramCliSetup.RunWizardAsync(
                    configuration,
                    configPath,
                    input,
                    output,
                    cancellationToken);

                await SaveConfigurationAsync(configPath, configuration, cancellationToken);
                telegramConfigured = TelegramCliSetup.HasConfiguredToken(configuration.Telegram);
                await output.WriteLineAsync();
            }
        }

        if (!options.NoStart)
        {
            if (await PromptYesNoAsync(input, output, "Start local agent now", defaultValue: true, cancellationToken))
            {
                var agent = await new AgentProcessManager().StartAsync(
                    new AgentProcessStartRequest(
                        options.ConfigPath,
                        options.ConnectionString,
                        options.MachineId,
                        options.MachineName,
                        options.Platform,
                        configuration.Runners.Default,
                        PollIntervalSeconds: 5,
                        QueuePreviewLimit: 100,
                        ClaimQueuedTask: true,
                        ExecuteRunner: true,
                        CreateWorktree: true),
                    cancellationToken);

                await WriteSetupProcessResultAsync("Agent", agent.Started, agent.AlreadyRunning, output);
            }

            if (telegramConfigured
                && await PromptYesNoAsync(input, output, "Start Telegram transport now", defaultValue: true, cancellationToken))
            {
                var telegram = await new TelegramProcessManager().StartAsync(
                    new TelegramProcessStartRequest(
                        options.ConfigPath,
                        options.ConnectionString,
                        PollLimit: 50,
                        TimeoutSeconds: 30),
                    cancellationToken);

                await WriteSetupProcessResultAsync("Telegram", telegram.Started, telegram.AlreadyRunning, output);
            }
            else if (!telegramConfigured && !options.SkipTelegram)
            {
                await output.WriteLineAsync("Telegram was not started because no bot token is configured.");
            }
        }

        await output.WriteLineAsync();
        await output.WriteLineAsync("Setup complete.");
        await output.WriteLineAsync("Run: aeges status");

        return 0;
    }

    private static async Task<AegesUpdatePlan> CreateUpdatePlanAsync(
        UpdateOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.PackageSource))
        {
            return new AegesUpdatePlan(
                options.ToolPackage,
                options.Version,
                Path.GetFullPath(options.PackageSource),
                DownloadedPackagePath: null);
        }

        if (options.DryRun)
        {
            return new AegesUpdatePlan(
                options.ToolPackage,
                options.Version ?? "latest",
                PackageSource: "(resolved from GitHub Releases)",
                DownloadedPackagePath: null);
        }

        return await DownloadUpdatePackageAsync(options, cancellationToken);
    }

    private static async Task<AegesUpdatePlan> DownloadUpdatePackageAsync(
        UpdateOptions options,
        CancellationToken cancellationToken)
    {
        var layout = RuntimeDirectoryLayout.CreateDefault();
        var downloadDirectory = Path.Combine(layout.RootPath, "tmp", "update");
        Directory.CreateDirectory(downloadDirectory);

        var downloadBaseUrl = options.DownloadBaseUrl;
        var version = options.Version;
        if (string.IsNullOrWhiteSpace(downloadBaseUrl))
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                version = await ResolveLatestGitHubReleaseVersionAsync(options.GithubRepository, cancellationToken);
            }

            downloadBaseUrl = $"https://github.com/{options.GithubRepository}/releases/download/v{version}";
        }

        using var client = new HttpClient();
        var checksums = await client.GetStringAsync(
            new Uri($"{downloadBaseUrl.TrimEnd('/')}/SHA256SUMS"),
            cancellationToken);
        var packageFile = string.IsNullOrWhiteSpace(version)
            ? ResolvePackageFileFromChecksums(checksums, options.ToolPackage)
            : $"{options.ToolPackage}.{version}.nupkg";
        version = packageFile[options.ToolPackage.Length..^".nupkg".Length].TrimStart('.');
        var packagePath = Path.Combine(downloadDirectory, packageFile);
        var expectedHash = ResolvePackageHash(checksums, packageFile);
        var packageBytes = await client.GetByteArrayAsync(
            new Uri($"{downloadBaseUrl.TrimEnd('/')}/{packageFile}"),
            cancellationToken);
        var actualHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();

        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Checksum verification failed for {packageFile}. Expected {expectedHash} but got {actualHash}.");
        }

        await File.WriteAllBytesAsync(packagePath, packageBytes, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(downloadDirectory, "SHA256SUMS"), checksums, cancellationToken);

        return new AegesUpdatePlan(options.ToolPackage, version, downloadDirectory, packagePath);
    }

    private static async Task<string> ResolveLatestGitHubReleaseVersionAsync(
        string githubRepository,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aeges.Cli");

        var releasesJson = await client.GetStringAsync(
            new Uri($"https://api.github.com/repos/{githubRepository}/releases?per_page=20"),
            cancellationToken);
        using var document = JsonDocument.Parse(releasesJson);

        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft)
                && draft.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            if (!release.TryGetProperty("tag_name", out var tagNameProperty))
            {
                continue;
            }

            var tagName = tagNameProperty.GetString();

            if (string.IsNullOrWhiteSpace(tagName))
            {
                continue;
            }

            var version = tagName.StartsWith("v", StringComparison.Ordinal)
                ? tagName[1..]
                : tagName;

            if (!release.TryGetProperty("assets", out var assets))
            {
                continue;
            }

            if (assets.EnumerateArray().Any(asset =>
                asset.TryGetProperty("name", out var name)
                && string.Equals(name.GetString(), $"Aeges.Cli.{version}.nupkg", StringComparison.Ordinal)))
            {
                return version;
            }
        }

        throw new InvalidOperationException(
            $"No published GitHub release with an Aeges.Cli package was found for {githubRepository}.");
    }

    private static string ResolvePackageFileFromChecksums(string checksums, string toolPackage)
    {
        foreach (var line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length >= 2
                && parts[1].StartsWith($"{toolPackage}.", StringComparison.Ordinal)
                && parts[1].EndsWith(".nupkg", StringComparison.Ordinal))
            {
                return parts[1];
            }
        }

        throw new InvalidOperationException($"SHA256SUMS does not contain a {toolPackage} package.");
    }

    private static string ResolvePackageHash(string checksums, string packageFile)
    {
        foreach (var line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length >= 2 && parts[1].Equals(packageFile, StringComparison.Ordinal))
            {
                return parts[0].ToLowerInvariant();
            }
        }

        throw new InvalidOperationException($"SHA256SUMS does not contain an entry for {packageFile}.");
    }

    private static async Task<DotnetToolResult> RunDotnetToolAsync(
        string verb,
        AegesUpdatePlan plan,
        CancellationToken cancellationToken)
    {
        var arguments = CreateDotnetToolArguments(verb, plan);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new DotnetToolResult(
            $"dotnet {string.Join(' ', arguments.Select(QuoteCommandArgument))}",
            process.ExitCode,
            stdout.Trim(),
            stderr.Trim());
    }

    private static IReadOnlyList<string> CreateDotnetToolArguments(
        string verb,
        AegesUpdatePlan plan)
    {
        var arguments = new List<string>
        {
            "tool",
            verb,
            "--global",
            plan.ToolPackage,
        };

        if (!string.IsNullOrWhiteSpace(plan.TargetVersion) && plan.TargetVersion != "latest")
        {
            arguments.Add("--version");
            arguments.Add(plan.TargetVersion);
        }

        arguments.Add("--add-source");
        arguments.Add(plan.PackageSource);

        return arguments;
    }

    private static async Task<WindowsDeferredUpdate> ScheduleWindowsDeferredUpdateAsync(
        AegesUpdatePlan plan,
        bool elevated,
        CancellationToken cancellationToken)
    {
        var layout = RuntimeDirectoryLayout.CreateDefault();
        var updateDirectory = Path.Combine(layout.RootPath, "tmp", "update");
        Directory.CreateDirectory(updateDirectory);
        Directory.CreateDirectory(layout.LogsPath);

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var scriptPath = Path.Combine(updateDirectory, $"aeges-update-{stamp}.ps1");
        var logPath = Path.Combine(layout.LogsPath, $"update-{stamp}.log");
        var processId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var updateArguments = FormatPowerShellArray(CreateDotnetToolArguments("update", plan));
        var installArguments = FormatPowerShellArray(CreateDotnetToolArguments("install", plan));
        var script = $$"""
            $ErrorActionPreference = "Continue"
            $logPath = {{FormatPowerShellString(logPath)}}
            $parentProcessId = {{processId}}

            function Write-AegesUpdateLog([string]$message) {
                $timestamp = Get-Date -Format "o"
                "$timestamp $message" | Out-File -FilePath $logPath -Append -Encoding utf8
            }

            Write-AegesUpdateLog "Waiting for aeges process $parentProcessId to exit."

            try {
                Wait-Process -Id $parentProcessId -ErrorAction SilentlyContinue
            }
            catch {
                Write-AegesUpdateLog "Wait-Process failed: $($_.Exception.Message)"
            }

            Start-Sleep -Milliseconds 500
            Write-AegesUpdateLog "Running dotnet tool update."

            & dotnet @({{updateArguments}}) >> $logPath 2>&1
            $updateExitCode = $LASTEXITCODE
            Write-AegesUpdateLog "dotnet tool update exited with $updateExitCode."

            if ($updateExitCode -ne 0) {
                Write-AegesUpdateLog "Running dotnet tool install fallback."
                & dotnet @({{installArguments}}) >> $logPath 2>&1
                $installExitCode = $LASTEXITCODE
                Write-AegesUpdateLog "dotnet tool install exited with $installExitCode."
                exit $installExitCode
            }

            exit $updateExitCode
            """;

        await File.WriteAllTextAsync(scriptPath, script, Encoding.UTF8, cancellationToken);

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = elevated,
        };

        if (elevated)
        {
            startInfo.Verb = "runas";
        }
        else
        {
            startInfo.CreateNoWindow = true;
        }

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start deferred Windows updater.");

        return new WindowsDeferredUpdate(scriptPath, logPath, elevated);
    }

    private static string FormatPowerShellArray(IReadOnlyList<string> values) =>
        string.Join(", ", values.Select(FormatPowerShellString));

    private static string FormatPowerShellString(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string QuoteCommandArgument(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private static async Task<int> RunLocalStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("status: building local runtime status");
        var status = await BuildLocalStatusAsync(options, cancellationToken);
        await TraceAsync($"status: database-up-to-date={status.Database.IsUpToDate}");
        await WriteLocalStatusAsync(status, options.Json, output);

        return status.Database.IsUpToDate ? 0 : 1;
    }

    private static async Task<int> RunTalkAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TalkCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var message = options.Message;

        if (string.IsNullOrWhiteSpace(message))
        {
            message = (await input.ReadToEndAsync(cancellationToken)).Trim();
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await error.WriteLineAsync("talk requires a message argument or stdin content.");
            return 2;
        }

        var configuration = LoadConfiguration(options);
        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(context);
        var clock = new SystemClock();
        var service = CreateTalkService(unitOfWork, clock, configuration);
        var result = await service.SendAsync(
            new SendTalkMessageRequest(
                "cli",
                message,
                options.SessionId is null ? null : new TalkSessionId(options.SessionId),
                options.StartNewSession),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await error.WriteLineAsync($"{result.Error!.Code}: {result.Error.Message}");
            return 1;
        }

        await WriteTalkExchangeAsync(result.Value!, options.Json, output);

        return 0;
    }

    private static async Task<int> RunDatabaseStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("db status: reading migration status");
        var service = CreateMigrationService(options);
        var status = await service.GetStatusAsync(cancellationToken);
        await WriteStatusAsync(status, options.Json, output);

        return 0;
    }

    private static async Task<int> RunAgentRunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = AgentRunCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("agent run: creating local runtime");
        var runtime = new LocalAgentRuntime();
        var agentOptions = CreateAgentRunOptions(options);
        await TraceAsync($"agent run: machine={agentOptions.MachineId} runner={agentOptions.RunnerId} once={options.Once}");

        try
        {
            if (options.Once)
            {
                var snapshot = await runtime.RunOnceAsync(agentOptions, cancellationToken);
                await TraceAsync($"agent run: queued={snapshot.QueuedTaskCount} claimed={snapshot.ClaimedTaskId ?? "(none)"}");
                await WriteAgentSnapshotAsync(snapshot, options.Json, output);
                return 0;
            }

            await output.WriteLineAsync($"Agent running for machine '{agentOptions.MachineId}'. Press Ctrl+C to stop.");
            await WriteRuntimeLogAsync(
                output,
                "agent",
                "info",
                $"Started machine={agentOptions.MachineId} runner={agentOptions.RunnerId} pollInterval={options.PollInterval} claimQueued={agentOptions.ClaimQueuedTask} createWorktree={agentOptions.CreateWorktree} executeRunner={agentOptions.ExecuteRunner}");

            // The initial shell deliberately performs bounded heartbeats only.
            // Task dispatch will be layered in later behind governed workflow checks.
            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = await runtime.RunOnceAsync(agentOptions, cancellationToken);
                await WriteAgentSnapshotAsync(snapshot, options.Json, output);
                await Task.Delay(options.PollInterval, cancellationToken);
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteRuntimeLogAsync(output, "agent", "info", "Stopped by cancellation.");
            await output.WriteLineAsync("Agent stopped.");
            return 0;
        }
        catch (Exception exception)
        {
            await WriteRuntimeLogAsync(error, "agent", "error", $"Unhandled failure: {exception.GetType().Name}: {exception.Message}");
            await error.WriteLineAsync(exception.ToString());
            return 1;
        }
    }

    private static async Task<int> RunAgentStartAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = AgentProcessCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("agent start: starting background process");
        var manager = new AgentProcessManager();
        var result = await manager.StartAsync(
            new AgentProcessStartRequest(
                options.ConfigPath,
                options.ConnectionString,
                options.MachineId,
                options.MachineName,
                options.Platform,
                options.RunnerId,
                options.PollIntervalSeconds,
                options.QueuePreviewLimit,
                options.ClaimQueuedTask,
                options.ExecuteRunner,
                options.CreateWorktree),
            cancellationToken);

        await TraceAsync($"agent start: pid={result.Status.Metadata?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "(none)"} running={result.Status.IsRunning}");
        await WriteAgentProcessStartResultAsync(result, options.Json, output);

        return 0;
    }

    private static async Task<int> RunAgentRestartAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = AgentProcessCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("agent restart: stopping background process");
        var manager = new AgentProcessManager();
        await manager.StopAsync(cancellationToken);
        await TraceAsync("agent restart: starting background process");
        var result = await manager.StartAsync(
            new AgentProcessStartRequest(
                options.ConfigPath,
                options.ConnectionString,
                options.MachineId,
                options.MachineName,
                options.Platform,
                options.RunnerId,
                options.PollIntervalSeconds,
                options.QueuePreviewLimit,
                options.ClaimQueuedTask,
                options.ExecuteRunner,
                options.CreateWorktree),
            cancellationToken);

        await TraceAsync($"agent restart: pid={result.Status.Metadata?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "(none)"} running={result.Status.IsRunning}");
        await WriteAgentProcessRestartResultAsync(result, options.Json, output);

        return 0;
    }

    private static async Task<int> RunAgentStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("agent status: reading process metadata");
        var status = await new AgentProcessManager().GetStatusAsync(cancellationToken);
        await WriteAgentProcessStatusAsync(status, options.Json, output);

        return 0;
    }

    private static async Task<int> RunAgentStopAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("agent stop: stopping background process");
        var result = await new AgentProcessManager().StopAsync(cancellationToken);
        await WriteAgentProcessStopResultAsync(result, options.Json, output);

        return 0;
    }

    private static async Task<int> RunDatabaseMigrateAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("db migrate: applying migrations");
        var service = CreateMigrationService(options);
        var status = await service.MigrateAsync(cancellationToken);
        await TraceAsync($"db migrate: applied={status.AppliedMigrations.Count} pending={status.PendingMigrations.Count}");
        await WriteStatusAsync(status, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTelegramRunAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TelegramRunCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var configuration = LoadConfiguration(options);
        var configPath = ResolveConfigPath(options);
        if (!await TelegramCliSetup.EnsureTokenAsync(
            configuration.Telegram,
            input,
            error,
            options.Interactive && !options.Json,
            cancellationToken))
        {
            return 1;
        }

        await TraceAsync("telegram run: acquiring runtime lock");
        using var telegramRunLock = TelegramRuntimeLock.TryAcquire();

        if (telegramRunLock is null)
        {
            await error.WriteLineAsync(
                "Telegram transport is already running for this runtime. Stop it with 'aeges telegram stop' or close the foreground 'aeges telegram run' terminal.");
            return 1;
        }

        await TraceAsync("telegram run: opening database");
        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(context);
        var clock = new SystemClock();
        var facade = new TelegramApplicationFacade(
            new ProjectService(unitOfWork, clock),
            new ProjectGroupService(unitOfWork, clock),
            new MachineService(unitOfWork, clock),
            new TaskService(unitOfWork, clock),
            new TaskIterationService(unitOfWork, clock),
            new ArtifactService(unitOfWork, clock),
            new RunnerExecutionService(unitOfWork, clock),
            new ApprovalService(unitOfWork, clock),
            CreateTalkService(unitOfWork, clock, configuration),
            new TelegramUserService(unitOfWork, clock),
            new TelegramTaskBindingService(unitOfWork, clock),
            configuration,
            configPath);
        var callbackActions = new TransportCallbackActionService(unitOfWork, clock);
        var handler = new TelegramInteractionHandler(
            facade,
            configuration.Telegram,
            new TelegramCallbackRegistry(callbackActions, clock),
            GetVersion());
        var pollingOptions = new TelegramLongPollingOptions(options.Limit, options.TimeoutSeconds);

        try
        {
            var gateway = TelegramBotClientFactory.CreateGateway(configuration.Telegram, cancellationToken);
            var service = new TelegramLongPollingService(
                gateway,
                handler,
                logSink: (entry, token) => WriteTelegramRuntimeLogAsync(entry, output, error, token));

            if (options.Once)
            {
                await TraceAsync("telegram run: polling once");
                var result = await service.PollOnceAsync(null, pollingOptions, cancellationToken);
                await TraceAsync($"telegram run: processed-updates={result.ProcessedUpdates}");
                await WriteTelegramPollingResultAsync(result, options.Json, output);
                return 0;
            }

            await output.WriteLineAsync("Telegram transport running. Press Ctrl+C to stop.");
            await WriteRuntimeLogAsync(
                output,
                "telegram",
                "info",
                $"Started config={configPath} database={context.Database.GetDbConnection().DataSource} limit={options.Limit} timeoutSeconds={options.TimeoutSeconds}");

            await service.RunAsync(pollingOptions, cancellationToken);

            return 0;
        }
        catch (TelegramTransportException exception)
        {
            await WriteRuntimeLogAsync(error, "telegram", "error", $"Transport startup failed: {exception.Message}");
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteRuntimeLogAsync(output, "telegram", "info", "Stopped by cancellation.");
            await output.WriteLineAsync("Telegram transport stopped.");
            return 0;
        }
        catch (Exception exception)
        {
            await WriteRuntimeLogAsync(error, "telegram", "error", $"Unhandled failure: {exception.GetType().Name}: {exception.Message}");
            await error.WriteLineAsync(exception.ToString());
            return 1;
        }
    }

    private static async Task<int> RunTelegramStartAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TelegramProcessCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var configuration = LoadConfiguration(options);
        if (!await TelegramCliSetup.EnsureTokenAsync(
            configuration.Telegram,
            input,
            error,
            allowPrompt: false,
            cancellationToken))
        {
            return 1;
        }

        await TraceAsync("telegram start: starting background process");
        var manager = new TelegramProcessManager();
        var result = await manager.StartAsync(
            new TelegramProcessStartRequest(
                options.ConfigPath,
                options.ConnectionString,
                options.Limit,
                options.TimeoutSeconds),
            cancellationToken);

        await TraceAsync($"telegram start: pid={result.Status.Metadata?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "(none)"} running={result.Status.IsRunning}");
        await WriteTelegramProcessStartResultAsync(result, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTelegramRestartAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TelegramProcessCliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var configuration = LoadConfiguration(options);
        if (!await TelegramCliSetup.EnsureTokenAsync(
            configuration.Telegram,
            input,
            error,
            allowPrompt: false,
            cancellationToken))
        {
            return 1;
        }

        await TraceAsync("telegram restart: stopping background process");
        var manager = new TelegramProcessManager();
        await manager.StopAsync(cancellationToken);
        await TraceAsync("telegram restart: starting background process");
        var result = await manager.StartAsync(
            new TelegramProcessStartRequest(
                options.ConfigPath,
                options.ConnectionString,
                options.Limit,
                options.TimeoutSeconds),
            cancellationToken);

        await TraceAsync($"telegram restart: pid={result.Status.Metadata?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "(none)"} running={result.Status.IsRunning}");
        await WriteTelegramProcessRestartResultAsync(result, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTelegramStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("telegram status: reading process metadata");
        var status = await new TelegramProcessManager().GetStatusAsync(cancellationToken);
        await WriteTelegramProcessStatusAsync(status, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTelegramStopAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await TraceAsync("telegram stop: stopping background process");
        var result = await new TelegramProcessManager().StopAsync(cancellationToken);
        await WriteTelegramProcessStopResultAsync(result, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTelegramSetupAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        if (options.Json)
        {
            await error.WriteLineAsync("telegram setup is interactive and does not support --json.");
            return 2;
        }

        var configPath = ResolveConfigPath(options);
        var configuration = LoadConfiguration(options);
        await TelegramCliSetup.RunWizardAsync(
            configuration,
            configPath,
            input,
            output,
            cancellationToken);

        await SaveConfigurationAsync(configPath, configuration, cancellationToken);
        await output.WriteLineAsync($"Run: aeges telegram run --config {configPath}");

        return 0;
    }

    private static async Task<int> RunTelegramCheckAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        var configuration = LoadConfiguration(options);

        try
        {
            var gateway = TelegramBotClientFactory.CreateGateway(configuration.Telegram, cancellationToken);
            var identity = await gateway.GetIdentityAsync(cancellationToken);

            if (options.Json)
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(
                    identity,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }));

                return 0;
            }

            await output.WriteLineAsync("Telegram bot is reachable.");
            await output.WriteLineAsync($"Id: {identity.Id}");
            await output.WriteLineAsync($"Username: {identity.Username ?? "(none)"}");
            await output.WriteLineAsync($"Name: {identity.FirstName}");
            await output.WriteLineAsync($"Is bot: {identity.IsBot}");

            return 0;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync($"Telegram check failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunTaskCreateAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TaskCreateOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new TaskService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.CreateAsync(
            new CreateTaskRequest(
                new ProjectId(options.ProjectId!),
                new MachineId(options.MachineId!),
                options.Title!,
                options.Goal!,
                options.Priority,
                options.MaxIterations,
                options.TaskId is null ? null : new TaskId(options.TaskId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteTaskAsync(result.Value!, options.Json, output, "Created task");

        return 0;
    }

    private static async Task<int> RunProjectAddAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = ProjectAddOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.RegisterAsync(
            new RegisterProjectRequest(
                options.Name!,
                options.Path!,
                options.ProjectId is null ? null : new ProjectId(options.ProjectId),
                options.GroupId is null ? null : new ProjectGroupId(options.GroupId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteProjectAsync(result.Value!, options.Json, output, "Added project");

        return 0;
    }

    private static async Task<int> RunProjectListAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectService(new SqliteUnitOfWork(context), new SystemClock());
        var projects = await service.ListAsync(cancellationToken);
        await WriteProjectsAsync(projects, options.Json, output);

        return 0;
    }

    private static async Task<int> RunProjectGroupAddAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = ProjectGroupAddOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectGroupService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.RegisterAsync(
            new RegisterProjectGroupRequest(
                options.Name!,
                options.Path,
                options.GroupId is null ? null : new ProjectGroupId(options.GroupId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteProjectGroupAsync(result.Value!, options.Json, output, "Added group");

        return 0;
    }

    private static async Task<int> RunProjectGroupListAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectGroupService(new SqliteUnitOfWork(context), new SystemClock());
        var groups = await service.ListAsync(cancellationToken);
        await WriteProjectGroupsAsync(groups, options.Json, output);

        return 0;
    }

    private static async Task<int> RunProjectRootAddAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = ProjectRootAddOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectRootService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.RegisterAsync(
            new RegisterProjectRootRequest(
                options.Name!,
                options.Path!,
                options.RootId is null ? null : new ProjectRootId(options.RootId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteProjectRootAsync(result.Value!, options.Json, output, "Added root");

        return 0;
    }

    private static async Task<int> RunProjectRootListAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new ProjectRootService(new SqliteUnitOfWork(context), new SystemClock());
        var roots = await service.ListAsync(cancellationToken);
        await WriteProjectRootsAsync(roots, options.Json, output);

        return 0;
    }

    private static async Task<int> RunProjectRootScanAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = ProjectRootScanOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(context);
        var target = await ResolveProjectRootScanTargetAsync(unitOfWork, options, cancellationToken);

        if (!target.IsSuccess)
        {
            await WriteErrorAsync(target.Error!, options.Json, error);
            return 1;
        }

        var scan = await ScanProjectRootAsync(unitOfWork, target.Value!, options, cancellationToken);

        if (options.Apply)
        {
            if (scan.RootStatus == "discovered")
            {
                await unitOfWork.ProjectRoots.AddAsync(target.Value!.Root, cancellationToken);
                scan.RootStatus = "created";
            }

            foreach (var group in scan.Groups.Where(group => group.Status == "discovered"))
            {
                await unitOfWork.ProjectGroups.AddAsync(
                    RuntimeProjectGroup.Create(
                        new ProjectGroupId(group.GroupId),
                        group.Name,
                        DateTimeOffset.UtcNow,
                        group.Path),
                    cancellationToken);
                group.Status = "created";
            }

            foreach (var candidate in scan.Candidates.Where(candidate => candidate.ProjectId is null))
            {
                var project = RuntimeProject.Create(
                    CreateUniqueProjectId(candidate.Name, scan.ExistingProjectIds),
                    candidate.Name,
                    candidate.Path,
                    DateTimeOffset.UtcNow,
                    candidate.GroupId is null ? null : new ProjectGroupId(candidate.GroupId));

                scan.ExistingProjectIds.Add(project.Id);
                await unitOfWork.Projects.AddAsync(project, cancellationToken);
                candidate.ProjectId = project.Id.Value;
                candidate.Status = "created";
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await WriteProjectRootScanAsync(scan, options.Json, output);

        return 0;
    }

    private static async Task<int> RunMachineAddAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = MachineAddOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new MachineService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.RegisterAsync(
            new RegisterMachineRequest(
                options.Name!,
                options.Platform!,
                options.MachineId is null ? null : new MachineId(options.MachineId)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteMachineAsync(result.Value!, options.Json, output, "Added machine");

        return 0;
    }

    private static async Task<int> RunMachineListAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new MachineService(new SqliteUnitOfWork(context), new SystemClock());
        var machines = await service.ListAsync(cancellationToken);
        await WriteMachinesAsync(machines, options.Json, output);

        return 0;
    }

    private static async Task<int> RunTaskStatusAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TaskStatusOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new TaskService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.GetAsync(new TaskId(options.TaskId!), cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteTaskAsync(result.Value!, options.Json, output, "Task");

        return 0;
    }

    private static async Task<int> RunTaskCancelAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TaskStatusOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var service = new TaskService(new SqliteUnitOfWork(context), new SystemClock());
        var result = await service.CancelAsync(new TaskId(options.TaskId!), cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteTaskAsync(result.Value!, options.Json, output, "Cancelled task");

        return 0;
    }

    private static async Task<int> RunTaskContinueAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var options = TaskContinueOptions.Parse(args);

        if (options.Error is not null)
        {
            await error.WriteLineAsync(options.Error);
            return 2;
        }

        await using var context = await CreateReadyDbContextAsync(options, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(context);
        var clock = new SystemClock();
        var taskService = new TaskService(unitOfWork, clock);
        var taskId = new TaskId(options.TaskId!);
        var task = await taskService.GetAsync(taskId, cancellationToken);

        if (!task.IsSuccess)
        {
            await WriteErrorAsync(task.Error!, options.Json, error);
            return 1;
        }

        if (task.Value!.Status != RuntimeTaskStatus.Reviewing)
        {
            await WriteErrorAsync(
                new ApplicationError("task_not_reviewing", $"Task '{taskId}' is not waiting for review feedback."),
                options.Json,
                error);
            return 1;
        }

        if (task.Value.CurrentIteration >= task.Value.MaxIterations)
        {
            await WriteErrorAsync(
                new ApplicationError("iteration_limit_reached", $"Task '{taskId}' cannot continue because it reached {task.Value.MaxIterations} iterations."),
                options.Json,
                error);
            return 1;
        }

        var artifact = await RegisterReviewFeedbackArtifactAsync(
            unitOfWork,
            task.Value,
            options.Feedback!,
            cancellationToken);

        if (!artifact.IsSuccess)
        {
            await WriteErrorAsync(artifact.Error!, options.Json, error);
            return 1;
        }

        var result = await taskService.RequeueForRevisionAsync(taskId, cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteErrorAsync(result.Error!, options.Json, error);
            return 1;
        }

        await WriteTaskAsync(result.Value!, options.Json, output, "Continued task");

        return 0;
    }

    private static async Task<ApplicationResult<RuntimeArtifact>> RegisterReviewFeedbackArtifactAsync(
        SqliteUnitOfWork unitOfWork,
        RuntimeTask task,
        string feedback,
        CancellationToken cancellationToken)
    {
        var layout = RuntimeDirectoryLayout.CreateDefault();
        var iterations = await new TaskIterationService(unitOfWork, new SystemClock())
            .ListByTaskAsync(task.Id, cancellationToken);
        var latestIterationId = iterations
            .OrderByDescending(iteration => iteration.IterationNumber)
            .FirstOrDefault()
            ?.Id;
        var artifactId = ArtifactId.New();
        var content = CreateReviewFeedbackContent(task, feedback);
        var bytes = Encoding.UTF8.GetBytes(content);
        var relativePath = CreateReviewArtifactPath(task, artifactId);
        var fullPath = ResolveArtifactPath(layout, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken);

        return await new ArtifactService(unitOfWork, new SystemClock()).RegisterAsync(
            new RegisterArtifactRequest(
                task.Id,
                latestIterationId,
                ArtifactType.Review,
                relativePath,
                bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                artifactId),
            cancellationToken);
    }

    private static string CreateReviewFeedbackContent(RuntimeTask task, string feedback) =>
        $"""
        # Review Feedback

        Task ID: {task.Id}
        Previous iteration: {task.CurrentIteration}

        Feedback:
        {feedback.Trim()}
        """;

    private static string CreateReviewArtifactPath(RuntimeTask task, ArtifactId artifactId) =>
        string.Join(
            '/',
            SanitizePathSegment(task.ProjectId.Value),
            SanitizePathSegment(task.Id.Value),
            "review",
            $"{SanitizePathSegment(artifactId.Value)}.md");

    private static string ResolveArtifactPath(RuntimeDirectoryLayout layout, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(layout.ArtifactsPath, relativePath));
        var artifactRoot = Path.GetFullPath(layout.ArtifactsPath);

        if (Path.GetRelativePath(artifactRoot, fullPath).StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Review feedback artifact path escaped the configured artifact root.");
        }

        return fullPath;
    }

    private static string SanitizePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.ToString();
    }

    private static async Task<ApplicationResult<ProjectRootScanTarget>> ResolveProjectRootScanTargetAsync(
        IUnitOfWork unitOfWork,
        ProjectRootScanOptions options,
        CancellationToken cancellationToken)
    {
        var roots = await unitOfWork.ProjectRoots.ListAsync(cancellationToken);
        var input = options.RootInput!;

        if (Directory.Exists(input))
        {
            var path = Path.GetFullPath(input);
            var existingRoot = roots.FirstOrDefault(root => NormalizePathKey(root.Path) == NormalizePathKey(path));

            if (existingRoot is not null)
            {
                return ApplicationResult<ProjectRootScanTarget>.Success(new ProjectRootScanTarget(existingRoot, "existing"));
            }

            var rootName = new DirectoryInfo(path).Name;
            var root = RuntimeProjectRoot.Create(
                CreateUniqueProjectRootId(rootName, roots.Select(existing => existing.Id).ToHashSet()),
                rootName,
                path,
                DateTimeOffset.UtcNow);

            return ApplicationResult<ProjectRootScanTarget>.Success(new ProjectRootScanTarget(root, "discovered"));
        }

        var registeredRoot = await unitOfWork.ProjectRoots.GetByIdAsync(new ProjectRootId(input), cancellationToken);

        return registeredRoot is null
            ? ApplicationResult<ProjectRootScanTarget>.Failure(
                "project_root_not_found",
                $"'{input}' is not a registered root id and is not an existing directory.")
            : ApplicationResult<ProjectRootScanTarget>.Success(new ProjectRootScanTarget(registeredRoot, "existing"));
    }

    private static async Task<ProjectRootScanOutput> ScanProjectRootAsync(
        IUnitOfWork unitOfWork,
        ProjectRootScanTarget target,
        ProjectRootScanOptions options,
        CancellationToken cancellationToken)
    {
        var projects = await unitOfWork.Projects.ListAsync(cancellationToken);
        var groups = await unitOfWork.ProjectGroups.ListAsync(cancellationToken);
        var existingProjectIds = projects.Select(project => project.Id).ToHashSet();
        var existingGroupIds = groups.Select(group => group.Id).ToHashSet();
        var existingProjectPaths = projects.ToDictionary(
            project => NormalizePathKey(project.Path),
            project => project.Id.Value,
            StringComparer.Ordinal);
        var existingGroupsByPath = groups
            .Where(group => !group.IsArchived && !string.IsNullOrWhiteSpace(group.Path))
            .GroupBy(group => NormalizePathKey(group.Path!), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var scanGroups = new Dictionary<string, ProjectScanGroupOutput>(StringComparer.Ordinal);
        var candidates = DiscoverProjectDirectories(target.Root.Path, options.MaxDepth)
            .Select(projectPath => CreateScanCandidate(
                target.Root.Path,
                projectPath,
                groups,
                existingGroupsByPath,
                existingGroupIds,
                existingProjectPaths,
                scanGroups))
            .OrderBy(candidate => candidate.GroupId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProjectRootScanOutput(
            target.Root.Id.Value,
            target.Root.Name,
            target.Root.Path,
            target.RootStatus,
            options.MaxDepth,
            options.Apply,
            existingProjectIds,
            scanGroups.Values
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            candidates);
    }

    private static ProjectScanCandidateOutput CreateScanCandidate(
        string rootPath,
        string projectPath,
        IReadOnlyList<RuntimeProjectGroup> groups,
        IReadOnlyDictionary<string, RuntimeProjectGroup> existingGroupsByPath,
        ISet<ProjectGroupId> existingGroupIds,
        IReadOnlyDictionary<string, string> existingProjectPaths)
    {
        return CreateScanCandidate(
            rootPath,
            projectPath,
            groups,
            existingGroupsByPath,
            existingGroupIds,
            existingProjectPaths,
            new Dictionary<string, ProjectScanGroupOutput>(StringComparer.Ordinal));
    }

    private static ProjectScanCandidateOutput CreateScanCandidate(
        string rootPath,
        string projectPath,
        IReadOnlyList<RuntimeProjectGroup> groups,
        IReadOnlyDictionary<string, RuntimeProjectGroup> existingGroupsByPath,
        ISet<ProjectGroupId> existingGroupIds,
        IReadOnlyDictionary<string, string> existingProjectPaths,
        IDictionary<string, ProjectScanGroupOutput> scanGroups)
    {
        var name = new DirectoryInfo(projectPath).Name;
        var group = ResolveProjectGroup(projectPath, groups);
        var inferredGroupPath = group is null ? InferProjectGroupPath(rootPath, projectPath) : null;
        var groupId = group?.Id.Value;

        if (groupId is null && inferredGroupPath is not null)
        {
            var normalizedGroupPath = NormalizePathKey(inferredGroupPath);

            if (existingGroupsByPath.TryGetValue(normalizedGroupPath, out var existingGroup))
            {
                groupId = existingGroup.Id.Value;
                if (!scanGroups.ContainsKey(normalizedGroupPath))
                {
                    scanGroups.Add(
                        normalizedGroupPath,
                        new ProjectScanGroupOutput(
                        existingGroup.Name,
                        existingGroup.Path!,
                        existingGroup.Id.Value,
                        "existing"));
                }
            }
            else
            {
                if (!scanGroups.TryGetValue(normalizedGroupPath, out var discoveredGroup))
                {
                    var groupName = new DirectoryInfo(inferredGroupPath).Name;
                    var discoveredGroupId = CreateUniqueProjectGroupId(groupName, existingGroupIds);
                    existingGroupIds.Add(discoveredGroupId);
                    discoveredGroup = new ProjectScanGroupOutput(
                        groupName,
                        inferredGroupPath,
                        discoveredGroupId.Value,
                        "discovered");
                    scanGroups.Add(normalizedGroupPath, discoveredGroup);
                }

                groupId = discoveredGroup.GroupId;
            }
        }

        var normalizedPath = NormalizePathKey(projectPath);
        existingProjectPaths.TryGetValue(normalizedPath, out var existingProjectId);

        return new ProjectScanCandidateOutput(
            name,
            projectPath,
            groupId,
            existingProjectId is null ? "discovered" : "existing",
            existingProjectId);
    }

    private static string? InferProjectGroupPath(string rootPath, string projectPath)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var fullProjectPath = Path.GetFullPath(projectPath);
        var relativePath = Path.GetRelativePath(fullRootPath, fullProjectPath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
        {
            return null;
        }

        var groupPath = Path.Combine(fullRootPath, segments[0]);

        return LooksLikeProject(groupPath) ? null : groupPath;
    }

    private static RuntimeProjectGroup? ResolveProjectGroup(
        string projectPath,
        IReadOnlyList<RuntimeProjectGroup> groups)
    {
        return groups
            .Where(group => !group.IsArchived && !string.IsNullOrWhiteSpace(group.Path))
            .Select(group => new
            {
                Group = group,
                Path = Path.GetFullPath(group.Path!),
            })
            .Where(group => IsPathWithin(projectPath, group.Path))
            .OrderByDescending(group => group.Path.Length)
            .FirstOrDefault()
            ?.Group;
    }

    private static IReadOnlyList<string> DiscoverProjectDirectories(string rootPath, int maxDepth)
    {
        var rootFullPath = Path.GetFullPath(rootPath);
        var discovered = new List<string>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootFullPath, 0));

        while (queue.Count > 0)
        {
            var (currentPath, depth) = queue.Dequeue();

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(currentPath).Order(StringComparer.OrdinalIgnoreCase))
            {
                var directoryName = new DirectoryInfo(directory).Name;

                if (ShouldIgnoreDirectory(directoryName))
                {
                    continue;
                }

                var childDepth = depth + 1;

                if (LooksLikeProject(directory))
                {
                    discovered.Add(Path.GetFullPath(directory));
                    continue;
                }

                if (childDepth < maxDepth)
                {
                    queue.Enqueue((directory, childDepth));
                }
            }
        }

        return discovered;
    }

    private static bool LooksLikeProject(string path)
    {
        return Directory.Exists(Path.Combine(path, ".git"))
            || Directory.EnumerateFiles(path, "*.sln").Any()
            || Directory.EnumerateFiles(path, "*.csproj").Any()
            || File.Exists(Path.Combine(path, "package.json"))
            || File.Exists(Path.Combine(path, "pyproject.toml"))
            || File.Exists(Path.Combine(path, "Cargo.toml"))
            || File.Exists(Path.Combine(path, "go.mod"))
            || File.Exists(Path.Combine(path, "deno.json"))
            || File.Exists(Path.Combine(path, "deno.jsonc"));
    }

    private static bool ShouldIgnoreDirectory(string directoryName)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".aeges",
            ".git",
            "bin",
            "build",
            "dist",
            "node_modules",
            "obj",
            "vendor",
        };

        return ignored.Contains(directoryName);
    }

    private static bool IsPathWithin(string childPath, string parentPath)
    {
        var fullChildPath = Path.GetFullPath(childPath);
        var fullParentPath = Path.GetFullPath(parentPath);
        var relativePath = Path.GetRelativePath(fullParentPath, fullChildPath);

        return relativePath == "."
            || (!relativePath.StartsWith("..", StringComparison.Ordinal)
                && !Path.IsPathRooted(relativePath));
    }

    private static string NormalizePathKey(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static ProjectId CreateUniqueProjectId(string name, ISet<ProjectId> existingProjectIds)
    {
        var baseId = CreateStableIdentifier(name);
        var candidate = new ProjectId(baseId);

        for (var suffix = 2; existingProjectIds.Contains(candidate); suffix++)
        {
            candidate = new ProjectId($"{baseId}-{suffix}");
        }

        return candidate;
    }

    private static ProjectGroupId CreateUniqueProjectGroupId(string name, ISet<ProjectGroupId> existingGroupIds)
    {
        var baseId = CreateStableIdentifier(name);
        var candidate = new ProjectGroupId(baseId);

        for (var suffix = 2; existingGroupIds.Contains(candidate); suffix++)
        {
            candidate = new ProjectGroupId($"{baseId}-{suffix}");
        }

        return candidate;
    }

    private static ProjectRootId CreateUniqueProjectRootId(string name, ISet<ProjectRootId> existingRootIds)
    {
        var baseId = CreateStableIdentifier(name);
        var candidate = new ProjectRootId(baseId);

        for (var suffix = 2; existingRootIds.Contains(candidate); suffix++)
        {
            candidate = new ProjectRootId($"{baseId}-{suffix}");
        }

        return candidate;
    }

    private static async Task TraceAsync(string message)
    {
        if (CurrentTrace.Value is { } trace)
        {
            await trace.WriteAsync(message);
        }
    }

    private static string FormatTraceCommand(string[] args)
    {
        if (args.Length == 0)
        {
            return "(none)";
        }

        var redacted = new List<string>(args.Length);
        var redactNext = false;

        foreach (var arg in args)
        {
            if (redactNext)
            {
                redacted.Add("(redacted)");
                redactNext = false;
                continue;
            }

            redacted.Add(arg);

            if (arg is "--connection-string" or "--bot-token" or "--token")
            {
                redactNext = true;
            }
        }

        return string.Join(' ', redacted.Select(QuoteCommandArgument));
    }

    private static SqliteMigrationService CreateMigrationService(CliOptions options)
    {
        return new SqliteMigrationService(ResolveConnectionString(options));
    }

    private static async Task<AegesDbContext> CreateReadyDbContextAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var context = new AegesDbContext(AegesDbContextOptions.Create(ResolveConnectionString(options)));
        await SqlitePragmas.ApplyAsync(context, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        return context;
    }

    private static async Task<AegesDbContext> CreateDbContextAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var context = new AegesDbContext(AegesDbContextOptions.Create(ResolveConnectionString(options)));
        await SqlitePragmas.ApplyAsync(context, cancellationToken);

        return context;
    }

    private static string ResolveConnectionString(CliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return options.ConnectionString;
        }

        var layout = RuntimeDirectoryLayout.CreateDefault();
        var configuration = LoadConfiguration(options);

        var connectionString = configuration.Storage.ConnectionString;

        if (connectionString is null)
        {
            foreach (var directory in layout.RequiredDirectories)
            {
                Directory.CreateDirectory(directory);
            }

            connectionString = $"Data Source={layout.DatabasePath}";
        }

        return connectionString;
    }

    private static AegesConfiguration LoadConfiguration(CliOptions options) =>
        new AegesConfigurationLoader().Load(new AegesConfigurationLoaderOptions(options.ConfigPath));

    private static TalkService CreateTalkService(
        IUnitOfWork unitOfWork,
        IClock clock,
        AegesConfiguration configuration)
    {
        var layout = RuntimeDirectoryLayout.CreateDefault();

        foreach (var directory in layout.RequiredDirectories)
        {
            Directory.CreateDirectory(directory);
        }

        var runner = new CodexTalkRunner(new CodexRunnerOptions(
            configuration.Runners.Codex.Executable,
            Model: configuration.Runners.Codex.Model,
            ReasoningEffort: configuration.Runners.Codex.ReasoningEffort,
            SandboxMode: "read-only",
            BypassApprovalsAndSandbox: false));

        return new TalkService(
            unitOfWork,
            clock,
            runner,
            layout,
            TimeSpan.FromSeconds(configuration.Runners.Codex.TimeoutSeconds));
    }

    private static string GetVersion()
    {
        var assembly = typeof(AegesCli).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static bool IsCurrentVersion(string? targetVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(targetVersion) || targetVersion == "latest")
        {
            return false;
        }

        return string.Equals(
            GetComparableVersion(currentVersion),
            GetComparableVersion(targetVersion),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetComparableVersion(string version)
    {
        var comparable = version.Trim();

        if (comparable.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            comparable = comparable[1..];
        }

        var metadataIndex = comparable.IndexOf('+', StringComparison.Ordinal);

        return metadataIndex < 0 ? comparable : comparable[..metadataIndex];
    }

    private static async Task<LocalRuntimeStatusOutput> BuildLocalStatusAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var layout = RuntimeDirectoryLayout.CreateDefault();
        var configuration = LoadConfiguration(options);
        var database = await CreateMigrationService(options).GetStatusAsync(cancellationToken);
        var agent = await new AgentProcessManager().GetStatusAsync(cancellationToken);
        var telegram = await new TelegramProcessManager().GetStatusAsync(cancellationToken);
        var codex = new CodexRunnerCommandBuilder(new CodexRunnerOptions(
            configuration.Runners.Codex.Executable,
            Model: configuration.Runners.Codex.Model,
            ReasoningEffort: configuration.Runners.Codex.ReasoningEffort,
            SandboxMode: configuration.Runners.Codex.SandboxMode,
            BypassApprovalsAndSandbox: configuration.Runners.Codex.BypassApprovalsAndSandbox))
            .CheckAvailability();

        if (!database.IsUpToDate)
        {
            return LocalRuntimeStatusOutput.PendingDatabase(
                layout,
                ResolveConfigPath(options),
                database,
                agent,
                telegram,
                codex);
        }

        await using var context = await CreateDbContextAsync(options, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(context);
        var projectCount = (await unitOfWork.Projects.ListAsync(cancellationToken)).Count;
        var machineCount = (await unitOfWork.Machines.ListAsync(cancellationToken)).Count;
        var taskCounts = new List<TaskStatusCountOutput>();

        foreach (var status in Enum.GetValues<RuntimeTaskStatus>())
        {
            var tasks = await unitOfWork.Tasks.ListByStatusAsync(status, int.MaxValue, cancellationToken);
            taskCounts.Add(new TaskStatusCountOutput(status.ToStorageValue(), tasks.Count));
        }

        return LocalRuntimeStatusOutput.Ready(
            layout,
            ResolveConfigPath(options),
            database,
            agent,
            telegram,
            codex,
            projectCount,
            machineCount,
            taskCounts);
    }

    private static async Task<InitResultOutput> InitializeRuntimeAsync(
        InitOptions options,
        CancellationToken cancellationToken)
    {
        var layout = RuntimeDirectoryLayout.CreateDefault();
        var configuration = LoadConfiguration(options);
        var projectPath = Path.GetFullPath(options.ProjectPath ?? Environment.CurrentDirectory);
        var projectName = options.ProjectName ?? new DirectoryInfo(projectPath).Name;
        var projectId = new ProjectId(options.ProjectId ?? CreateStableIdentifier(projectName));
        var machineId = new MachineId(options.MachineId ?? configuration.MachineId);
        var machineName = options.MachineName ?? Environment.MachineName;
        var platform = options.Platform ?? RuntimeInformation.OSDescription;

        foreach (var directory in layout.RequiredDirectories)
        {
            Directory.CreateDirectory(directory);
        }

        var database = await CreateMigrationService(options).MigrateAsync(cancellationToken);
        await using var context = await CreateDbContextAsync(options, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(context);
        var clock = new SystemClock();
        var projectCreated = false;
        var machineCreated = false;

        var project = await unitOfWork.Projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            project = RuntimeProject.Create(projectId, projectName, projectPath, clock.Now);
            await unitOfWork.Projects.AddAsync(project, cancellationToken);
            projectCreated = true;
        }

        var machine = await unitOfWork.Machines.GetByIdAsync(machineId, cancellationToken);
        if (machine is null)
        {
            machine = RuntimeMachine.Create(machineId, machineName, platform, clock.Now);
            await unitOfWork.Machines.AddAsync(machine, cancellationToken);
            machineCreated = true;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new InitResultOutput(
            layout.RootPath,
            ResolveConfigPath(options),
            database.DatabasePath,
            database.IsUpToDate,
            ProjectOutput.From(project),
            projectCreated,
            MachineOutput.From(machine),
            machineCreated,
            [
                "aeges status",
                "aeges telegram setup",
                "aeges telegram start",
                "aeges agent start",
            ]);
    }

    private static string CreateStableIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (character is '-' or '_' or '.')
            {
                builder.Append('-');
            }
            else if (char.IsWhiteSpace(character))
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');

        return string.IsNullOrWhiteSpace(result) ? "project" : result;
    }

    private static string ResolveConfigPath(CliOptions options)
    {
        if (options.ConfigPath is null)
        {
            return RuntimeDirectoryLayout.CreateDefault().ConfigPath;
        }

        if (!Path.IsPathFullyQualified(options.ConfigPath))
        {
            throw new ArgumentException("Configuration path must be absolute.", nameof(options));
        }

        return Path.GetFullPath(options.ConfigPath);
    }

    private static async Task SaveConfigurationAsync(
        string configPath,
        AegesConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(configPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(
            stream,
            configuration,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            },
            cancellationToken);

        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }

    private static async Task<bool> PromptYesNoAsync(
        TextReader input,
        TextWriter output,
        string label,
        bool defaultValue,
        CancellationToken cancellationToken)
    {
        var suffix = defaultValue ? "Y/n" : "y/N";

        while (true)
        {
            await output.WriteAsync($"{label} [{suffix}]: ");
            var value = await input.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            value = value.Trim();

            if (value.Equals("y", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("n", StringComparison.OrdinalIgnoreCase)
                || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            await output.WriteLineAsync("Enter yes or no.");
        }
    }

    private static AgentRunOptions CreateAgentRunOptions(AgentRunCliOptions options)
    {
        var configuration = LoadConfiguration(options);
        var layout = RuntimeDirectoryLayout.CreateDefault();

        return new AgentRunOptions(
            ResolveConnectionString(options),
            options.MachineId ?? configuration.MachineId,
            options.MachineName ?? Environment.MachineName,
            options.Platform ?? RuntimeInformation.OSDescription,
            options.QueuePreviewLimit,
            options.RunnerId ?? configuration.Runners.Default,
            layout.RootPath,
            TimeSpan.FromSeconds(configuration.Runners.Codex.TimeoutSeconds),
            options.ClaimQueuedTask,
            options.ExecuteRunner,
            options.CreateWorktree,
            configuration.Runners.Codex.Executable,
            configuration.Runners.Codex.Model,
            configuration.Runners.Codex.ReasoningEffort,
            configuration.Runners.Codex.SandboxMode,
            configuration.Runners.Codex.BypassApprovalsAndSandbox);
    }

    private static async Task WriteStatusAsync(
        SqliteMigrationStatus status,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                status,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Database: {status.DatabasePath ?? "(unknown)"}");
        await output.WriteLineAsync($"Status: {(status.IsUpToDate ? "up-to-date" : "pending migrations")}");
        await output.WriteLineAsync($"Applied migrations: {status.AppliedMigrations.Count}");
        foreach (var migration in status.AppliedMigrations)
        {
            await output.WriteLineAsync($"  - {migration}");
        }

        await output.WriteLineAsync($"Pending migrations: {status.PendingMigrations.Count}");
        foreach (var migration in status.PendingMigrations)
        {
            await output.WriteLineAsync($"  - {migration}");
        }
    }

    private static async Task WriteLocalStatusAsync(
        LocalRuntimeStatusOutput status,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                status,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

            await output.WriteLineAsync("Aeges local status");
        await output.WriteLineAsync($"Version: {status.Version}");
        await output.WriteLineAsync($"Runtime: {status.RuntimeRootPath}");
        await output.WriteLineAsync($"Config: {status.ConfigPath}");
        await output.WriteLineAsync($"Database: {status.Database.DatabasePath ?? "(unknown)"}");
        await output.WriteLineAsync($"Database status: {(status.Database.IsUpToDate ? "up-to-date" : "pending migrations")}");
        await output.WriteLineAsync($"Agent: {status.Agent.Status}");
        await output.WriteLineAsync($"Telegram: {status.Telegram.Status}");
        await output.WriteLineAsync($"Codex: {(status.Codex.IsAvailable ? "available" : "missing")}");

        if (!string.IsNullOrWhiteSpace(status.Codex.ResolvedPath))
        {
            await output.WriteLineAsync($"Codex path: {status.Codex.ResolvedPath}");
        }

        if (!status.Codex.IsAvailable)
        {
            await output.WriteLineAsync($"Codex help: {status.Codex.InstallationUrl}");
        }

        if (!status.Database.IsUpToDate)
        {
            await output.WriteLineAsync("Next step: aeges setup");
            return;
        }

        await output.WriteLineAsync($"Projects: {status.ProjectCount}");
        await output.WriteLineAsync($"Machines: {status.MachineCount}");
        await output.WriteLineAsync("Tasks:");

        foreach (var count in status.TaskCounts)
        {
            await output.WriteLineAsync($"  - {count.Status}: {count.Count}");
        }
    }

    private static async Task WriteInitResultAsync(
        InitResultOutput result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync("Aeges initialized.");
        await output.WriteLineAsync($"Runtime: {result.RuntimeRootPath}");
        await output.WriteLineAsync($"Config: {result.ConfigPath}");
        await output.WriteLineAsync($"Database: {result.DatabasePath ?? "(unknown)"}");
        await output.WriteLineAsync($"Database status: {(result.DatabaseUpToDate ? "up-to-date" : "pending migrations")}");
        await output.WriteLineAsync($"Project: {result.Project.Id} ({(result.ProjectCreated ? "created" : "existing")})");
        await output.WriteLineAsync($"Project path: {result.Project.Path}");
        await output.WriteLineAsync($"Machine: {result.Machine.Id} ({(result.MachineCreated ? "created" : "existing")})");
        await output.WriteLineAsync("Next steps:");

        foreach (var command in result.NextSteps)
        {
            await output.WriteLineAsync($"  - {command}");
        }
    }

    private static async Task WriteTalkExchangeAsync(
        TalkExchange exchange,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                TalkExchangeOutput.From(exchange),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync(exchange.AssistantMessage.Content);
        await output.WriteLineAsync();
        await output.WriteLineAsync($"Talk session: {exchange.Session.Id}");
        await output.WriteLineAsync($"Runner: {exchange.Session.RunnerId}");
    }

    private static async Task WriteTaskAsync(
        RuntimeTask task,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                TaskOutput.From(task),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {task.Id}");
        await output.WriteLineAsync($"Project: {task.ProjectId}");
        await output.WriteLineAsync($"Machine: {task.MachineId}");
        await output.WriteLineAsync($"Title: {task.Title}");
        await output.WriteLineAsync($"Status: {task.Status.ToStorageValue()}");
        await output.WriteLineAsync($"Priority: {task.Priority}");
        await output.WriteLineAsync($"Iterations: {task.CurrentIteration}/{task.MaxIterations}");
        await output.WriteLineAsync($"Created: {task.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {task.UpdatedAt:O}");

        if (!string.IsNullOrWhiteSpace(task.FailureReason))
        {
            await output.WriteLineAsync($"Failure: {task.FailureReason}");
        }
    }

    private static async Task WriteProjectAsync(
        RuntimeProject project,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                ProjectOutput.From(project),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {project.Id}");
        await output.WriteLineAsync($"Name: {project.Name}");
        await output.WriteLineAsync($"Status: {(project.IsArchived ? "archived" : "active")}");
        await output.WriteLineAsync($"Group: {project.GroupId?.Value ?? "ungrouped"}");
        await output.WriteLineAsync($"Path: {project.Path}");
        await output.WriteLineAsync($"Created: {project.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {project.UpdatedAt:O}");
        if (project.ArchivedAt is not null)
        {
            await output.WriteLineAsync($"Archived: {project.ArchivedAt:O}");
        }
    }

    private static async Task WriteProjectGroupAsync(
        RuntimeProjectGroup group,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                ProjectGroupOutput.From(group),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {group.Id}");
        await output.WriteLineAsync($"Name: {group.Name}");
        await output.WriteLineAsync($"Status: {(group.IsArchived ? "archived" : "active")}");
        await output.WriteLineAsync($"Path: {group.Path ?? "(none)"}");
        await output.WriteLineAsync($"Created: {group.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {group.UpdatedAt:O}");
    }

    private static async Task WriteProjectGroupsAsync(
        IReadOnlyList<RuntimeProjectGroup> groups,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                groups.Select(ProjectGroupOutput.From).ToArray(),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Groups: {groups.Count}");

        foreach (var group in groups)
        {
            await output.WriteLineAsync(
                $"  - {group.Id} | {group.Name} | {(group.IsArchived ? "archived" : "active")} | {group.Path ?? "(none)"}");
        }
    }

    private static async Task WriteProjectRootAsync(
        RuntimeProjectRoot root,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                ProjectRootOutput.From(root),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {root.Id}");
        await output.WriteLineAsync($"Name: {root.Name}");
        await output.WriteLineAsync($"Status: {(root.IsArchived ? "archived" : "active")}");
        await output.WriteLineAsync($"Path: {root.Path}");
        await output.WriteLineAsync($"Created: {root.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {root.UpdatedAt:O}");
    }

    private static async Task WriteProjectRootsAsync(
        IReadOnlyList<RuntimeProjectRoot> roots,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                roots.Select(ProjectRootOutput.From).ToArray(),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Roots: {roots.Count}");

        foreach (var root in roots)
        {
            await output.WriteLineAsync(
                $"  - {root.Id} | {root.Name} | {(root.IsArchived ? "archived" : "active")} | {root.Path}");
        }
    }

    private static async Task WriteProjectRootScanAsync(
        ProjectRootScanOutput scan,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                scan,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Root scan: {scan.RootId} ({scan.RootStatus})");
        await output.WriteLineAsync($"Path: {scan.RootPath}");
        await output.WriteLineAsync($"Groups: {scan.Groups.Count}");
        foreach (var group in scan.Groups)
        {
            await output.WriteLineAsync(
                $"  - {group.Status} | {group.GroupId} | {group.Name} | {group.Path}");
        }

        await output.WriteLineAsync($"Candidates: {scan.Candidates.Count}");

        foreach (var candidate in scan.Candidates)
        {
            await output.WriteLineAsync(
                $"  - {candidate.Status} | {candidate.Name} | group: {candidate.GroupId ?? "ungrouped"} | {candidate.Path}");
        }
    }

    private static async Task WriteProjectsAsync(
        IReadOnlyList<RuntimeProject> projects,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                projects.Select(ProjectOutput.From).ToArray(),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Projects: {projects.Count}");

        foreach (var project in projects)
        {
            await output.WriteLineAsync(
                $"  - {project.Id} | {project.Name} | {(project.IsArchived ? "archived" : "active")} | group: {project.GroupId?.Value ?? "ungrouped"} | {project.Path}");
        }
    }

    private static async Task WriteMachineAsync(
        RuntimeMachine machine,
        bool json,
        TextWriter output,
        string heading)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                MachineOutput.From(machine),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{heading}: {machine.Id}");
        await output.WriteLineAsync($"Name: {machine.Name}");
        await output.WriteLineAsync($"Platform: {machine.Platform}");
        await output.WriteLineAsync($"Status: {machine.Status.ToStorageValue()}");
        await output.WriteLineAsync(
            $"Last seen: {(machine.LastSeenAt is null ? "(never)" : machine.LastSeenAt.Value.ToString("O"))}");
        await output.WriteLineAsync($"Created: {machine.CreatedAt:O}");
        await output.WriteLineAsync($"Updated: {machine.UpdatedAt:O}");
    }

    private static async Task WriteMachinesAsync(
        IReadOnlyList<RuntimeMachine> machines,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                machines.Select(MachineOutput.From).ToArray(),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Machines: {machines.Count}");

        foreach (var machine in machines)
        {
            await output.WriteLineAsync(
                $"  - {machine.Id} | {machine.Name} | {machine.Platform} | {machine.Status.ToStorageValue()}");
        }
    }

    private static async Task WriteAgentSnapshotAsync(
        AgentRunSnapshot snapshot,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                AgentSnapshotOutput.From(snapshot),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Agent heartbeat: {snapshot.MachineId}");
        await output.WriteLineAsync($"Database: {snapshot.DatabasePath ?? "(unknown)"}");
        await output.WriteLineAsync($"Queued tasks: {snapshot.QueuedTaskCount}");
        await output.WriteLineAsync($"Claimed task: {snapshot.ClaimedTaskId ?? "(none)"}");
        await output.WriteLineAsync($"Created iteration: {snapshot.CreatedIterationId ?? "(none)"}");
        await output.WriteLineAsync($"Prompt artifact: {snapshot.PromptArtifactId ?? "(none)"}");
        await output.WriteLineAsync($"Prompt path: {snapshot.PromptPath ?? "(none)"}");
        await output.WriteLineAsync($"Worktree path: {snapshot.WorktreePath ?? "(none)"}");
        await output.WriteLineAsync($"Artifact output: {snapshot.ArtifactOutputDirectory ?? "(none)"}");
        await output.WriteLineAsync($"Runner execution: {snapshot.RunnerExecutionId ?? "(none)"}");
        await output.WriteLineAsync($"Runner status: {snapshot.RunnerStatus ?? "(none)"}");
        await output.WriteLineAsync(
            $"Runner exit code: {(snapshot.RunnerExitCode is null ? "(none)" : snapshot.RunnerExitCode.Value.ToString())}");
        await output.WriteLineAsync($"Runner error: {snapshot.RunnerErrorSummary ?? "(none)"}");
        await output.WriteLineAsync($"Worktree created: {snapshot.WorktreeCreated}");
        await output.WriteLineAsync($"Worktree base commit: {snapshot.WorktreeBaseCommit ?? "(none)"}");
        await output.WriteLineAsync($"Heartbeat: {snapshot.HeartbeatAt:O}");
    }

    private static async Task WriteAgentProcessStartResultAsync(
        AgentProcessStartResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                AgentProcessStatusOutput.From(result.Status),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync(result.AlreadyRunning
            ? "Agent worker is already running."
            : "Agent worker started.");
        await WriteAgentProcessStatusLinesAsync(result.Status, output);
    }

    private static async Task WriteAgentProcessRestartResultAsync(
        AgentProcessStartResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                AgentProcessStatusOutput.From(result.Status),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync(result.AlreadyRunning
            ? "Agent worker is already running."
            : "Agent worker restarted.");
        await WriteAgentProcessStatusLinesAsync(result.Status, output);
    }

    private static async Task WriteAgentProcessStatusAsync(
        AgentProcessStatus status,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                AgentProcessStatusOutput.From(status),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await WriteAgentProcessStatusLinesAsync(status, output);
    }

    private static async Task WriteAgentProcessStopResultAsync(
        AgentProcessStopResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                AgentProcessStatusOutput.From(result.PreviousStatus),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync(result.Stopped
            ? "Agent worker stopped."
            : "Agent worker was not running.");
        await output.WriteLineAsync($"Metadata: {result.PreviousStatus.MetadataPath}");
    }

    private static async Task WriteAgentProcessStatusLinesAsync(
        AgentProcessStatus status,
        TextWriter output)
    {
        var state = status.IsRunning ? "running" : status.IsStale ? "stale" : "stopped";
        await output.WriteLineAsync($"Status: {state}");
        await output.WriteLineAsync($"Metadata: {status.MetadataPath}");

        if (status.Metadata is null)
        {
            return;
        }

        await output.WriteLineAsync($"PID: {status.Metadata.ProcessId}");
        await output.WriteLineAsync($"Started: {status.Metadata.StartedAt:O}");
        await output.WriteLineAsync($"Stdout: {status.Metadata.StdoutPath}");
        await output.WriteLineAsync($"Stderr: {status.Metadata.StderrPath}");
    }

    private static async Task WriteTelegramPollingResultAsync(
        TelegramLongPollingResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"Processed updates: {result.ProcessedUpdates}");
        await output.WriteLineAsync(
            $"Next offset: {(result.NextOffset is null ? "(none)" : result.NextOffset.Value.ToString())}");
    }

    private static async ValueTask WriteTelegramRuntimeLogAsync(
        TelegramLongPollingLogEntry entry,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var writer = entry.Level == TelegramLongPollingLogLevel.Warning
            ? error
            : output;
        var details = new List<string>();

        if (entry.NextOffset is not null)
        {
            details.Add($"nextOffset={entry.NextOffset.Value}");
        }

        if (entry.ProcessedUpdates is not null)
        {
            details.Add($"processed={entry.ProcessedUpdates.Value}");
        }

        if (!string.IsNullOrWhiteSpace(entry.ExceptionType))
        {
            details.Add($"exception={entry.ExceptionType}");
        }

        if (!string.IsNullOrWhiteSpace(entry.ErrorMessage))
        {
            details.Add($"error={entry.ErrorMessage}");
        }

        var message = details.Count == 0
            ? entry.Message
            : $"{entry.Message} {string.Join(' ', details)}";
        var level = entry.Level == TelegramLongPollingLogLevel.Warning
            ? "warn"
            : "info";

        await WriteRuntimeLogAsync(writer, "telegram", level, message);
    }

    private static async Task WriteRuntimeLogAsync(
        TextWriter writer,
        string component,
        string level,
        string message)
    {
        await writer.WriteLineAsync($"{DateTimeOffset.UtcNow:O} [{component}] {level}: {message}");
        await writer.FlushAsync();
    }

    private static async Task WriteTelegramProcessStartResultAsync(
        TelegramProcessStartResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                TelegramProcessStatusOutput.From(result.Status),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        if (result.AlreadyRunning)
        {
            await output.WriteLineAsync("Telegram transport is already running.");
        }
        else
        {
            await output.WriteLineAsync("Telegram transport started.");
        }

        await WriteTelegramProcessStatusLinesAsync(result.Status, output);
    }

    private static async Task WriteTelegramProcessRestartResultAsync(
        TelegramProcessStartResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                TelegramProcessStatusOutput.From(result.Status),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync(result.AlreadyRunning
            ? "Telegram transport is already running."
            : "Telegram transport restarted.");
        await WriteTelegramProcessStatusLinesAsync(result.Status, output);
    }

    private static async Task WriteTelegramProcessStatusAsync(
        TelegramProcessStatus status,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                TelegramProcessStatusOutput.From(status),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await WriteTelegramProcessStatusLinesAsync(status, output);
    }

    private static async Task WriteTelegramProcessStopResultAsync(
        TelegramProcessStopResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                TelegramProcessStatusOutput.From(result.PreviousStatus),
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync(result.Stopped
            ? "Telegram transport stopped."
            : "Telegram transport was not running.");
        await output.WriteLineAsync($"Metadata: {result.PreviousStatus.MetadataPath}");
    }

    private static async Task WriteTelegramProcessStatusLinesAsync(
        TelegramProcessStatus status,
        TextWriter output)
    {
        var state = status.IsRunning ? "running" : status.IsStale ? "stale" : "stopped";
        await output.WriteLineAsync($"Status: {state}");
        await output.WriteLineAsync($"Metadata: {status.MetadataPath}");

        if (status.Metadata is null)
        {
            return;
        }

        await output.WriteLineAsync($"PID: {status.Metadata.ProcessId}");
        await output.WriteLineAsync($"Started: {status.Metadata.StartedAt:O}");
        await output.WriteLineAsync($"Stdout: {status.Metadata.StdoutPath}");
        await output.WriteLineAsync($"Stderr: {status.Metadata.StderrPath}");
    }

    private static async Task WriteErrorAsync(
        ApplicationError errorValue,
        bool json,
        TextWriter error)
    {
        if (json)
        {
            await error.WriteLineAsync(JsonSerializer.Serialize(
                errorValue,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await error.WriteLineAsync($"{errorValue.Code}: {errorValue.Message}");
    }

    private static async Task WriteVersionAsync(
        VersionOutput version,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                version,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        await output.WriteLineAsync($"{version.Name} {version.Version}");
    }

    private static async Task WriteUpdateProgressAsync(
        AegesUpdatePlan plan,
        TextWriter output)
    {
        await output.WriteLineAsync("Updating Aeges runtime...");
        await output.WriteLineAsync($"Package: {plan.ToolPackage}");
        await output.WriteLineAsync($"Version: {plan.TargetVersion ?? "latest"}");
        await output.WriteLineAsync($"Source: {plan.PackageSource}");

        if (!string.IsNullOrWhiteSpace(plan.DownloadedPackagePath))
        {
            await output.WriteLineAsync($"Downloaded: {plan.DownloadedPackagePath}");
        }
    }

    private static async Task WriteUpdateResultAsync(
        AegesUpdateResult result,
        bool json,
        TextWriter output)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));

            return;
        }

        if (result.DryRun)
        {
            await output.WriteLineAsync("Aeges update dry run.");
            await output.WriteLineAsync($"Current version: {result.CurrentVersion}");
            await output.WriteLineAsync($"Target version: {result.TargetVersion}");
            await output.WriteLineAsync($"Package: {result.ToolPackage}");
            await output.WriteLineAsync($"Source: {result.PackageSource}");
            return;
        }

        if (result.UpToDate)
        {
            await output.WriteLineAsync("Aeges is already up to date.");
            await output.WriteLineAsync($"Current version: {result.CurrentVersion}");
            await output.WriteLineAsync($"Target version: {result.TargetVersion}");
            await output.WriteLineAsync($"Source: {result.PackageSource}");
            return;
        }

        if (result.DeferredUpdate)
        {
            await output.WriteLineAsync(result.Elevated
                ? "Aeges update was handed off to an elevated Windows updater."
                : "Aeges update was handed off to a deferred Windows updater.");
            await output.WriteLineAsync($"Current version: {result.CurrentVersion}");
            await output.WriteLineAsync($"Target version: {result.TargetVersion}");
            await output.WriteLineAsync($"Log: {result.DeferredLogPath}");
            await output.WriteLineAsync($"Script: {result.DeferredScriptPath}");
            await output.WriteLineAsync("The updater waits for this aeges process to exit before replacing the global tool.");
            await output.WriteLineAsync("If access is still denied, stop background processes first:");
            await output.WriteLineAsync("  aeges agent stop");
            await output.WriteLineAsync("  aeges telegram stop");
            await output.WriteLineAsync("Then run: aeges update");
            await output.WriteLineAsync("Use --elevated only when your user profile .dotnet folder has broken permissions.");
            return;
        }

        await output.WriteLineAsync(result.Updated ? "Aeges updated." : "Aeges update failed.");
        await output.WriteLineAsync($"Current process version: {result.CurrentVersion}");
        await output.WriteLineAsync($"Target version: {result.TargetVersion}");

        if (!string.IsNullOrWhiteSpace(result.UpdateStandardOutput))
        {
            await output.WriteLineAsync(result.UpdateStandardOutput);
        }

        if (!string.IsNullOrWhiteSpace(result.UpdateStandardError))
        {
            await output.WriteLineAsync(result.UpdateStandardError);
        }

        if (!string.IsNullOrWhiteSpace(result.InstallStandardOutput))
        {
            await output.WriteLineAsync(result.InstallStandardOutput);
        }

        if (!string.IsNullOrWhiteSpace(result.InstallStandardError))
        {
            await output.WriteLineAsync(result.InstallStandardError);
        }

        if (result.Updated)
        {
            await output.WriteLineAsync("Restart background processes to load the updated runtime:");
            await output.WriteLineAsync("  aeges agent restart");
            await output.WriteLineAsync("  aeges telegram restart");
        }
    }

    private static async Task WriteSetupProcessResultAsync(
        string name,
        bool started,
        bool alreadyRunning,
        TextWriter output)
    {
        var status = started
            ? "started"
            : alreadyRunning
                ? "already running"
                : "not started";

        await output.WriteLineAsync($"{name}: {status}");
    }

    private static async Task WriteUsageAsync(TextWriter error)
    {
        await error.WriteLineAsync("Usage:");
        await error.WriteLineAsync("  Add -v or --verbose to any command to print execution trace lines to stderr.");
        await error.WriteLineAsync("  aeges version [--json]");
        await error.WriteLineAsync("  aeges update [--version <version>] [--package-source <path>] [--download-base-url <url>] [--github-repository <owner/repo>] [--dry-run] [--direct] [--elevated] [--json]");
        await error.WriteLineAsync("  aeges setup [--project-id <id>] [--project-name <name>] [--path <path>] [--machine-id <id>] [--machine-name <name>] [--platform <text>] [--skip-telegram] [--no-start] [--config <path>] [--connection-string <value>]");
        await error.WriteLineAsync("  aeges init [--project-id <id>] [--project-name <name>] [--path <path>] [--machine-id <id>] [--machine-name <name>] [--platform <text>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges status [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges talk [message] [--new] [--session-id <id>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges db status [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges db migrate [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges project add --name <name> --path <path> [--project-id <id>] [--group-id <id>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges project list [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges group add --name <name> [--path <path>] [--group-id <id>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges group list [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges root add --name <name> --path <path> [--root-id <id>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges root list [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges root scan <path-or-root-id> [--max-depth <int>] [--recursive] [--apply] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges machine add --name <name> --platform <text> [--machine-id <id>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges machine list [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges task create --project-id <id> --machine-id <id> --title <title> --goal <goal> [--task-id <id>] [--priority <int>] [--max-iterations <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges task status <task-id> [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges task cancel <task-id> [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges task continue <task-id> --feedback <text> [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges agent run [--once] [--machine-id <id>] [--machine-name <name>] [--platform <text>] [--runner-id <id>] [--no-claim] [--create-worktree] [--execute-runner] [--poll-interval-seconds <int>] [--queue-preview-limit <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges agent start [--machine-id <id>] [--runner-id <id>] [--no-claim] [--no-create-worktree] [--no-execute-runner] [--poll-interval-seconds <int>] [--queue-preview-limit <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges agent restart [--machine-id <id>] [--runner-id <id>] [--no-claim] [--no-create-worktree] [--no-execute-runner] [--poll-interval-seconds <int>] [--queue-preview-limit <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges agent status [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges agent stop [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges telegram setup [--config <path>]");
        await error.WriteLineAsync("  aeges telegram check [--config <path>] [--json]");
        await error.WriteLineAsync("  aeges telegram run [--once] [--no-interactive] [--poll-limit <int>] [--timeout-seconds <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges telegram start [--poll-limit <int>] [--timeout-seconds <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges telegram restart [--poll-limit <int>] [--timeout-seconds <int>] [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges telegram status [--config <path>] [--connection-string <value>] [--json]");
        await error.WriteLineAsync("  aeges telegram stop [--config <path>] [--connection-string <value>] [--json]");
    }

    private class CliOptions
    {
        public string? ConfigPath { get; protected init; }

        public string? ConnectionString { get; protected init; }

        public bool Json { get; protected init; }

        public string? Error { get; protected init; }

        public static CliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return new CliOptions { Error = "--config requires a value." };
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return new CliOptions { Error = "--connection-string requires a value." };
                        }

                        break;
                    default:
                        return new CliOptions { Error = $"Unknown option '{args[index]}'." };
                }
            }

            return new CliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
            };
        }

        protected static bool TryReadValue(string[] args, ref int index, out string? value)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = null;
                return false;
            }

            index++;
            value = args[index];

            return true;
        }
    }

    private sealed record CliTraceOptions(bool Verbose, string[] Args)
    {
        public static CliTraceOptions Parse(string[] args)
        {
            var stripped = new List<string>(args.Length);
            var verbose = false;

            foreach (var arg in args)
            {
                if (arg is "-v" or "--verbose")
                {
                    verbose = true;
                    continue;
                }

                stripped.Add(arg);
            }

            return new CliTraceOptions(verbose, [.. stripped]);
        }
    }

    private sealed class CliTrace
    {
        private readonly bool enabled;
        private readonly TextWriter writer;

        public CliTrace(bool enabled, TextWriter writer)
        {
            this.enabled = enabled;
            this.writer = writer;
        }

        public async Task WriteAsync(string message)
        {
            if (!enabled)
            {
                return;
            }

            await writer.WriteLineAsync($"trace: {message}");
        }
    }

    private sealed class TalkCliOptions : CliOptions
    {
        public string? Message { get; private init; }

        public string? SessionId { get; private init; }

        public bool StartNewSession { get; private init; }

        public new static TalkCliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? message = null;
            string? sessionId = null;
            var startNewSession = false;
            var json = false;
            var messageParts = new List<string>();

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--new":
                        startNewSession = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--session-id":
                        if (!TryReadValue(args, ref index, out sessionId))
                        {
                            return ErrorResult("--session-id requires a value.");
                        }

                        break;
                    case "--message":
                        if (!TryReadValue(args, ref index, out message))
                        {
                            return ErrorResult("--message requires a value.");
                        }

                        break;
                    default:
                        if (args[index].StartsWith("--", StringComparison.Ordinal))
                        {
                            return ErrorResult($"Unknown option '{args[index]}'.");
                        }

                        messageParts.Add(args[index]);
                        break;
                }
            }

            if (message is not null && messageParts.Count > 0)
            {
                return ErrorResult("Use either positional message text or --message, not both.");
            }

            return new TalkCliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                Message = message ?? (messageParts.Count == 0 ? null : string.Join(' ', messageParts)),
                SessionId = sessionId,
                StartNewSession = startNewSession,
            };
        }

        private static TalkCliOptions ErrorResult(string error) => new() { Error = error };
    }

    private class InitOptions : CliOptions
    {
        public string? ProjectId { get; protected init; }

        public string? ProjectName { get; protected init; }

        public string? ProjectPath { get; protected init; }

        public string? MachineId { get; protected init; }

        public string? MachineName { get; protected init; }

        public string? Platform { get; protected init; }

        public new static InitOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? projectId = null;
            string? projectName = null;
            string? projectPath = null;
            string? machineId = null;
            string? machineName = null;
            string? platform = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--project-id":
                        if (!TryReadValue(args, ref index, out projectId))
                        {
                            return ErrorResult("--project-id requires a value.");
                        }

                        break;
                    case "--project-name":
                        if (!TryReadValue(args, ref index, out projectName))
                        {
                            return ErrorResult("--project-name requires a value.");
                        }

                        break;
                    case "--path":
                        if (!TryReadValue(args, ref index, out projectPath))
                        {
                            return ErrorResult("--path requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--machine-name":
                        if (!TryReadValue(args, ref index, out machineName))
                        {
                            return ErrorResult("--machine-name requires a value.");
                        }

                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref index, out platform))
                        {
                            return ErrorResult("--platform requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new InitOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                ProjectId = projectId,
                ProjectName = projectName,
                ProjectPath = projectPath,
                MachineId = machineId,
                MachineName = machineName,
                Platform = platform,
            };
        }

        private static InitOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class SetupOptions : InitOptions
    {
        public bool SkipTelegram { get; private init; }

        public bool NoStart { get; private init; }

        public new static SetupOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? projectId = null;
            string? projectName = null;
            string? projectPath = null;
            string? machineId = null;
            string? machineName = null;
            string? platform = null;
            var skipTelegram = false;
            var noStart = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        return ErrorResult("setup is interactive and does not support --json. Use 'aeges init --json' for scripted initialization.");
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--project-id":
                        if (!TryReadValue(args, ref index, out projectId))
                        {
                            return ErrorResult("--project-id requires a value.");
                        }

                        break;
                    case "--project-name":
                        if (!TryReadValue(args, ref index, out projectName))
                        {
                            return ErrorResult("--project-name requires a value.");
                        }

                        break;
                    case "--path":
                        if (!TryReadValue(args, ref index, out projectPath))
                        {
                            return ErrorResult("--path requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--machine-name":
                        if (!TryReadValue(args, ref index, out machineName))
                        {
                            return ErrorResult("--machine-name requires a value.");
                        }

                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref index, out platform))
                        {
                            return ErrorResult("--platform requires a value.");
                        }

                        break;
                    case "--skip-telegram":
                        skipTelegram = true;
                        break;
                    case "--no-start":
                        noStart = true;
                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new SetupOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                ProjectId = projectId,
                ProjectName = projectName,
                ProjectPath = projectPath,
                MachineId = machineId,
                MachineName = machineName,
                Platform = platform,
                SkipTelegram = skipTelegram,
                NoStart = noStart,
            };
        }

        private static SetupOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class UpdateOptions : CliOptions
    {
        public string? Version { get; private init; }

        public string? PackageSource { get; private init; }

        public string? DownloadBaseUrl { get; private init; }

        public string GithubRepository { get; private init; } = "ai-iskuzhin/aeges";

        public string ToolPackage { get; private init; } = "Aeges.Cli";

        public bool DryRun { get; private init; }

        public bool Direct { get; private init; }

        public bool Elevated { get; private init; }

        public new static UpdateOptions Parse(string[] args)
        {
            string? version = null;
            string? packageSource = null;
            string? downloadBaseUrl = null;
            var githubRepository = "ai-iskuzhin/aeges";
            var toolPackage = "Aeges.Cli";
            var dryRun = false;
            var direct = false;
            var elevated = false;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--dry-run":
                        dryRun = true;
                        break;
                    case "--direct":
                        direct = true;
                        break;
                    case "--elevated":
                        elevated = true;
                        break;
                    case "--version":
                        if (!TryReadValue(args, ref index, out version))
                        {
                            return ErrorResult("--version requires a value.");
                        }

                        break;
                    case "--package-source":
                    case "--source":
                        var sourceOption = args[index];
                        if (!TryReadValue(args, ref index, out packageSource))
                        {
                            return ErrorResult($"{sourceOption} requires a value.");
                        }

                        break;
                    case "--download-base-url":
                        if (!TryReadValue(args, ref index, out downloadBaseUrl))
                        {
                            return ErrorResult("--download-base-url requires a value.");
                        }

                        break;
                    case "--github-repository":
                        if (!TryReadValue(args, ref index, out githubRepository))
                        {
                            return ErrorResult("--github-repository requires a value.");
                        }

                        break;
                    case "--tool-package":
                        if (!TryReadValue(args, ref index, out toolPackage))
                        {
                            return ErrorResult("--tool-package requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new UpdateOptions
            {
                Json = json,
                Version = version,
                PackageSource = packageSource,
                DownloadBaseUrl = downloadBaseUrl,
                GithubRepository = githubRepository!,
                ToolPackage = toolPackage!,
                DryRun = dryRun,
                Direct = direct,
                Elevated = elevated,
            };
        }

        private static UpdateOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class TaskCreateOptions : CliOptions
    {
        public string? ProjectId { get; private init; }

        public string? MachineId { get; private init; }

        public string? TaskId { get; private init; }

        public string? Title { get; private init; }

        public string? Goal { get; private init; }

        public int Priority { get; private init; }

        public int MaxIterations { get; private init; } = 3;

        public new static TaskCreateOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? projectId = null;
            string? machineId = null;
            string? taskId = null;
            string? title = null;
            string? goal = null;
            var priority = 0;
            var maxIterations = 3;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--project-id":
                        if (!TryReadValue(args, ref index, out projectId))
                        {
                            return ErrorResult("--project-id requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--task-id":
                        if (!TryReadValue(args, ref index, out taskId))
                        {
                            return ErrorResult("--task-id requires a value.");
                        }

                        break;
                    case "--title":
                        if (!TryReadValue(args, ref index, out title))
                        {
                            return ErrorResult("--title requires a value.");
                        }

                        break;
                    case "--goal":
                        if (!TryReadValue(args, ref index, out goal))
                        {
                            return ErrorResult("--goal requires a value.");
                        }

                        break;
                    case "--priority":
                        if (!TryReadInt(args, ref index, out priority))
                        {
                            return ErrorResult("--priority requires an integer value.");
                        }

                        break;
                    case "--max-iterations":
                        if (!TryReadInt(args, ref index, out maxIterations) || maxIterations <= 0)
                        {
                            return ErrorResult("--max-iterations requires an integer value greater than zero.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(projectId, "--project-id")
                ?? RequireText(machineId, "--machine-id")
                ?? RequireText(title, "--title")
                ?? RequireText(goal, "--goal")
                ?? new TaskCreateOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    ProjectId = projectId,
                    MachineId = machineId,
                    TaskId = taskId,
                    Title = title,
                    Goal = goal,
                    Priority = priority,
                    MaxIterations = maxIterations,
                };
        }

        private static bool TryReadInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text))
            {
                value = 0;
                return false;
            }

            return int.TryParse(text, out value);
        }

        private static TaskCreateOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static TaskCreateOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class ProjectAddOptions : CliOptions
    {
        public string? ProjectId { get; private init; }

        public string? GroupId { get; private init; }

        public string? Name { get; private init; }

        public string? Path { get; private init; }

        public new static ProjectAddOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? projectId = null;
            string? groupId = null;
            string? name = null;
            string? path = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--project-id":
                        if (!TryReadValue(args, ref index, out projectId))
                        {
                            return ErrorResult("--project-id requires a value.");
                        }

                        break;
                    case "--group-id":
                    case "--group":
                        if (!TryReadValue(args, ref index, out groupId))
                        {
                            return ErrorResult($"{args[index]} requires a value.");
                        }

                        break;
                    case "--name":
                        if (!TryReadValue(args, ref index, out name))
                        {
                            return ErrorResult("--name requires a value.");
                        }

                        break;
                    case "--path":
                        if (!TryReadValue(args, ref index, out path))
                        {
                            return ErrorResult("--path requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(name, "--name")
                ?? RequireText(path, "--path")
                ?? new ProjectAddOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    ProjectId = projectId,
                    GroupId = groupId,
                    Name = name,
                    Path = path,
                };
        }

        private static ProjectAddOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static ProjectAddOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class ProjectGroupAddOptions : CliOptions
    {
        public string? GroupId { get; private init; }

        public string? Name { get; private init; }

        public string? Path { get; private init; }

        public new static ProjectGroupAddOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? groupId = null;
            string? name = null;
            string? path = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--group-id":
                        if (!TryReadValue(args, ref index, out groupId))
                        {
                            return ErrorResult("--group-id requires a value.");
                        }

                        break;
                    case "--name":
                        if (!TryReadValue(args, ref index, out name))
                        {
                            return ErrorResult("--name requires a value.");
                        }

                        break;
                    case "--path":
                        if (!TryReadValue(args, ref index, out path))
                        {
                            return ErrorResult("--path requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(name, "--name")
                ?? new ProjectGroupAddOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    GroupId = groupId,
                    Name = name,
                    Path = path,
                };
        }

        private static ProjectGroupAddOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static ProjectGroupAddOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class ProjectRootAddOptions : CliOptions
    {
        public string? RootId { get; private init; }

        public string? Name { get; private init; }

        public string? Path { get; private init; }

        public new static ProjectRootAddOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? rootId = null;
            string? name = null;
            string? path = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--root-id":
                        if (!TryReadValue(args, ref index, out rootId))
                        {
                            return ErrorResult("--root-id requires a value.");
                        }

                        break;
                    case "--name":
                        if (!TryReadValue(args, ref index, out name))
                        {
                            return ErrorResult("--name requires a value.");
                        }

                        break;
                    case "--path":
                        if (!TryReadValue(args, ref index, out path))
                        {
                            return ErrorResult("--path requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(name, "--name")
                ?? RequireText(path, "--path")
                ?? new ProjectRootAddOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    RootId = rootId,
                    Name = name,
                    Path = path,
                };
        }

        private static ProjectRootAddOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static ProjectRootAddOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class ProjectRootScanOptions : CliOptions
    {
        public string? RootInput { get; private init; }

        public int MaxDepth { get; private init; } = 2;

        public bool Apply { get; private init; }

        public new static ProjectRootScanOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? rootInput = null;
            int? maxDepth = null;
            var recursive = false;
            var apply = false;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--root-id":
                        if (!TryReadValue(args, ref index, out rootInput))
                        {
                            return ErrorResult("--root-id requires a value.");
                        }

                        break;
                    case "--max-depth":
                        if (!TryReadPositiveInt(args, ref index, out var value))
                        {
                            return ErrorResult("--max-depth requires a positive integer.");
                        }

                        maxDepth = value;
                        break;
                    case "--recursive":
                        recursive = true;
                        break;
                    case "--apply":
                        apply = true;
                        break;
                    default:
                        if (args[index].StartsWith("--", StringComparison.Ordinal))
                        {
                            return ErrorResult($"Unknown option '{args[index]}'.");
                        }

                        if (rootInput is not null)
                        {
                            return ErrorResult("Only one root path or identifier can be supplied.");
                        }

                        rootInput = args[index];
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(rootInput))
            {
                return ErrorResult("Root path or identifier is required.");
            }

            return new ProjectRootScanOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                RootInput = rootInput,
                MaxDepth = maxDepth ?? (recursive ? 3 : 2),
                Apply = apply,
            };
        }

        private static bool TryReadPositiveInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text) || !int.TryParse(text, out value))
            {
                value = 0;
                return false;
            }

            return value > 0;
        }

        private static ProjectRootScanOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class MachineAddOptions : CliOptions
    {
        public string? MachineId { get; private init; }

        public string? Name { get; private init; }

        public string? Platform { get; private init; }

        public new static MachineAddOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? machineId = null;
            string? name = null;
            string? platform = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--name":
                        if (!TryReadValue(args, ref index, out name))
                        {
                            return ErrorResult("--name requires a value.");
                        }

                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref index, out platform))
                        {
                            return ErrorResult("--platform requires a value.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return RequireText(name, "--name")
                ?? RequireText(platform, "--platform")
                ?? new MachineAddOptions
                {
                    ConfigPath = configPath,
                    ConnectionString = connectionString,
                    Json = json,
                    MachineId = machineId,
                    Name = name,
                    Platform = platform,
                };
        }

        private static MachineAddOptions? RequireText(string? value, string optionName) =>
            string.IsNullOrWhiteSpace(value)
                ? ErrorResult($"{optionName} is required.")
                : null;

        private static MachineAddOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class TaskStatusOptions : CliOptions
    {
        public string? TaskId { get; private init; }

        public new static TaskStatusOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? taskId = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--task-id":
                        if (!TryReadValue(args, ref index, out taskId))
                        {
                            return ErrorResult("--task-id requires a value.");
                        }

                        break;
                    default:
                        if (args[index].StartsWith("--", StringComparison.Ordinal))
                        {
                            return ErrorResult($"Unknown option '{args[index]}'.");
                        }

                        if (taskId is not null)
                        {
                            return ErrorResult("Only one task identifier can be supplied.");
                        }

                        taskId = args[index];
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ErrorResult("Task identifier is required.");
            }

            return new TaskStatusOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                TaskId = taskId,
            };
        }

        private static TaskStatusOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class TaskContinueOptions : CliOptions
    {
        public string? TaskId { get; private init; }

        public string? Feedback { get; private init; }

        public new static TaskContinueOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? taskId = null;
            string? feedback = null;
            var json = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--feedback":
                        if (!TryReadValue(args, ref index, out feedback))
                        {
                            return ErrorResult("--feedback requires a value.");
                        }

                        break;
                    case "--task-id":
                        if (!TryReadValue(args, ref index, out taskId))
                        {
                            return ErrorResult("--task-id requires a value.");
                        }

                        break;
                    default:
                        if (args[index].StartsWith("--", StringComparison.Ordinal))
                        {
                            return ErrorResult($"Unknown option '{args[index]}'.");
                        }

                        if (taskId is not null)
                        {
                            return ErrorResult("Only one task identifier can be supplied.");
                        }

                        taskId = args[index];
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ErrorResult("Task identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(feedback))
            {
                return ErrorResult("--feedback is required.");
            }

            return new TaskContinueOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                TaskId = taskId,
                Feedback = feedback,
            };
        }

        private static TaskContinueOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class AgentRunCliOptions : CliOptions
    {
        public bool Once { get; private init; }

        public string? MachineId { get; private init; }

        public string? MachineName { get; private init; }

        public string? Platform { get; private init; }

        public TimeSpan PollInterval { get; private init; } = TimeSpan.FromSeconds(5);

        public int QueuePreviewLimit { get; private init; } = 100;

        public string? RunnerId { get; private init; }

        public bool ClaimQueuedTask { get; private init; } = true;

        public bool ExecuteRunner { get; private init; }

        public bool CreateWorktree { get; private init; }

        public new static AgentRunCliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? machineId = null;
            string? machineName = null;
            string? platform = null;
            string? runnerId = null;
            var once = false;
            var json = false;
            var pollInterval = TimeSpan.FromSeconds(5);
            var queuePreviewLimit = 100;
            var claimQueuedTask = true;
            var executeRunner = false;
            var createWorktree = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--once":
                        once = true;
                        break;
                    case "--json":
                        json = true;
                        break;
                    case "--no-claim":
                        claimQueuedTask = false;
                        break;
                    case "--execute-runner":
                        executeRunner = true;
                        break;
                    case "--create-worktree":
                        createWorktree = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--machine-name":
                        if (!TryReadValue(args, ref index, out machineName))
                        {
                            return ErrorResult("--machine-name requires a value.");
                        }

                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref index, out platform))
                        {
                            return ErrorResult("--platform requires a value.");
                        }

                        break;
                    case "--runner-id":
                        if (!TryReadValue(args, ref index, out runnerId))
                        {
                            return ErrorResult("--runner-id requires a value.");
                        }

                        break;
                    case "--poll-interval-seconds":
                        if (!TryReadPositiveInt(args, ref index, out var seconds))
                        {
                            return ErrorResult("--poll-interval-seconds requires an integer value greater than zero.");
                        }

                        pollInterval = TimeSpan.FromSeconds(seconds);
                        break;
                    case "--queue-preview-limit":
                        if (!TryReadPositiveInt(args, ref index, out queuePreviewLimit))
                        {
                            return ErrorResult("--queue-preview-limit requires an integer value greater than zero.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new AgentRunCliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                Once = once,
                MachineId = machineId,
                MachineName = machineName,
                Platform = platform,
                PollInterval = pollInterval,
                QueuePreviewLimit = queuePreviewLimit,
                RunnerId = runnerId,
                ClaimQueuedTask = claimQueuedTask,
                ExecuteRunner = executeRunner,
                CreateWorktree = createWorktree,
            };
        }

        private static bool TryReadPositiveInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text) || !int.TryParse(text, out value))
            {
                value = 0;
                return false;
            }

            return value > 0;
        }

        private static AgentRunCliOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class AgentProcessCliOptions : CliOptions
    {
        public string? MachineId { get; private init; }

        public string? MachineName { get; private init; }

        public string? Platform { get; private init; }

        public int PollIntervalSeconds { get; private init; } = 5;

        public int QueuePreviewLimit { get; private init; } = 100;

        public string? RunnerId { get; private init; }

        public bool ClaimQueuedTask { get; private init; } = true;

        public bool ExecuteRunner { get; private init; } = true;

        public bool CreateWorktree { get; private init; } = true;

        public new static AgentProcessCliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            string? machineId = null;
            string? machineName = null;
            string? platform = null;
            string? runnerId = null;
            var json = false;
            var pollIntervalSeconds = 5;
            var queuePreviewLimit = 100;
            var claimQueuedTask = true;
            var executeRunner = true;
            var createWorktree = true;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--no-claim":
                        claimQueuedTask = false;
                        break;
                    case "--no-execute-runner":
                        executeRunner = false;
                        break;
                    case "--no-create-worktree":
                        createWorktree = false;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--machine-id":
                        if (!TryReadValue(args, ref index, out machineId))
                        {
                            return ErrorResult("--machine-id requires a value.");
                        }

                        break;
                    case "--machine-name":
                        if (!TryReadValue(args, ref index, out machineName))
                        {
                            return ErrorResult("--machine-name requires a value.");
                        }

                        break;
                    case "--platform":
                        if (!TryReadValue(args, ref index, out platform))
                        {
                            return ErrorResult("--platform requires a value.");
                        }

                        break;
                    case "--runner-id":
                        if (!TryReadValue(args, ref index, out runnerId))
                        {
                            return ErrorResult("--runner-id requires a value.");
                        }

                        break;
                    case "--poll-interval-seconds":
                        if (!TryReadPositiveInt(args, ref index, out pollIntervalSeconds))
                        {
                            return ErrorResult("--poll-interval-seconds requires an integer value greater than zero.");
                        }

                        break;
                    case "--queue-preview-limit":
                        if (!TryReadPositiveInt(args, ref index, out queuePreviewLimit))
                        {
                            return ErrorResult("--queue-preview-limit requires an integer value greater than zero.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new AgentProcessCliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                MachineId = machineId,
                MachineName = machineName,
                Platform = platform,
                PollIntervalSeconds = pollIntervalSeconds,
                QueuePreviewLimit = queuePreviewLimit,
                RunnerId = runnerId,
                ClaimQueuedTask = claimQueuedTask,
                ExecuteRunner = executeRunner,
                CreateWorktree = createWorktree,
            };
        }

        private static bool TryReadPositiveInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text) || !int.TryParse(text, out value))
            {
                value = 0;
                return false;
            }

            return value > 0;
        }

        private static AgentProcessCliOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class TelegramRunCliOptions : CliOptions
    {
        public bool Once { get; private init; }

        public int Limit { get; private init; } = 50;

        public int TimeoutSeconds { get; private init; } = 30;

        public bool Interactive { get; private init; } = true;

        public new static TelegramRunCliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            var once = false;
            var json = false;
            var limit = 50;
            var timeoutSeconds = 30;
            var interactive = true;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--once":
                        once = true;
                        break;
                    case "--no-interactive":
                        interactive = false;
                        break;
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--poll-limit":
                        if (!TryReadPositiveInt(args, ref index, out limit))
                        {
                            return ErrorResult("--poll-limit requires an integer value greater than zero.");
                        }

                        break;
                    case "--timeout-seconds":
                        if (!TryReadPositiveInt(args, ref index, out timeoutSeconds))
                        {
                            return ErrorResult("--timeout-seconds requires an integer value greater than zero.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new TelegramRunCliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                Once = once,
                Limit = limit,
                TimeoutSeconds = timeoutSeconds,
                Interactive = interactive,
            };
        }

        private static bool TryReadPositiveInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text) || !int.TryParse(text, out value))
            {
                value = 0;
                return false;
            }

            return value > 0;
        }

        private static TelegramRunCliOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed class TelegramProcessCliOptions : CliOptions
    {
        public int Limit { get; private init; } = 50;

        public int TimeoutSeconds { get; private init; } = 30;

        public new static TelegramProcessCliOptions Parse(string[] args)
        {
            string? configPath = null;
            string? connectionString = null;
            var json = false;
            var limit = 50;
            var timeoutSeconds = 30;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--config":
                        if (!TryReadValue(args, ref index, out configPath))
                        {
                            return ErrorResult("--config requires a value.");
                        }

                        break;
                    case "--connection-string":
                        if (!TryReadValue(args, ref index, out connectionString))
                        {
                            return ErrorResult("--connection-string requires a value.");
                        }

                        break;
                    case "--poll-limit":
                        if (!TryReadPositiveInt(args, ref index, out limit))
                        {
                            return ErrorResult("--poll-limit requires an integer value greater than zero.");
                        }

                        break;
                    case "--timeout-seconds":
                        if (!TryReadPositiveInt(args, ref index, out timeoutSeconds))
                        {
                            return ErrorResult("--timeout-seconds requires an integer value greater than zero.");
                        }

                        break;
                    default:
                        return ErrorResult($"Unknown option '{args[index]}'.");
                }
            }

            return new TelegramProcessCliOptions
            {
                ConfigPath = configPath,
                ConnectionString = connectionString,
                Json = json,
                Limit = limit,
                TimeoutSeconds = timeoutSeconds,
            };
        }

        private static bool TryReadPositiveInt(string[] args, ref int index, out int value)
        {
            if (!TryReadValue(args, ref index, out var text) || !int.TryParse(text, out value))
            {
                value = 0;
                return false;
            }

            return value > 0;
        }

        private static TelegramProcessCliOptions ErrorResult(string error) => new() { Error = error };
    }

    private sealed record ProjectOutput(
        string Id,
        string Name,
        string Path,
        string? GroupId,
        bool IsArchived,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static ProjectOutput From(RuntimeProject project) =>
            new(
                project.Id.Value,
                project.Name,
                project.Path,
                project.GroupId?.Value,
                project.IsArchived,
                project.ArchivedAt,
                project.CreatedAt,
                project.UpdatedAt);
    }

    private sealed record ProjectGroupOutput(
        string Id,
        string Name,
        string? Path,
        bool IsArchived,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static ProjectGroupOutput From(RuntimeProjectGroup group) =>
            new(
                group.Id.Value,
                group.Name,
                group.Path,
                group.IsArchived,
                group.ArchivedAt,
                group.CreatedAt,
                group.UpdatedAt);
    }

    private sealed record ProjectRootOutput(
        string Id,
        string Name,
        string Path,
        bool IsArchived,
        DateTimeOffset? ArchivedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static ProjectRootOutput From(RuntimeProjectRoot root) =>
            new(
                root.Id.Value,
                root.Name,
                root.Path,
                root.IsArchived,
                root.ArchivedAt,
                root.CreatedAt,
                root.UpdatedAt);
    }

    private sealed record ProjectRootScanTarget(RuntimeProjectRoot Root, string RootStatus);

    private sealed class ProjectRootScanOutput
    {
        public ProjectRootScanOutput(
            string rootId,
            string rootName,
            string rootPath,
            string rootStatus,
            int maxDepth,
            bool applied,
            ISet<ProjectId> existingProjectIds,
            IReadOnlyList<ProjectScanGroupOutput> groups,
            IReadOnlyList<ProjectScanCandidateOutput> candidates)
        {
            RootId = rootId;
            RootName = rootName;
            RootPath = rootPath;
            RootStatus = rootStatus;
            MaxDepth = maxDepth;
            Applied = applied;
            ExistingProjectIds = existingProjectIds;
            Groups = groups;
            Candidates = candidates;
        }

        public string RootId { get; }

        public string RootName { get; }

        public string RootPath { get; }

        public string RootStatus { get; set; }

        public int MaxDepth { get; }

        public bool Applied { get; }

        [JsonIgnore]
        public ISet<ProjectId> ExistingProjectIds { get; }

        public IReadOnlyList<ProjectScanGroupOutput> Groups { get; }

        public IReadOnlyList<ProjectScanCandidateOutput> Candidates { get; }
    }

    private sealed class ProjectScanGroupOutput
    {
        public ProjectScanGroupOutput(
            string name,
            string path,
            string groupId,
            string status)
        {
            Name = name;
            Path = path;
            GroupId = groupId;
            Status = status;
        }

        public string Name { get; }

        public string Path { get; }

        public string GroupId { get; }

        public string Status { get; set; }
    }

    private sealed class ProjectScanCandidateOutput
    {
        public ProjectScanCandidateOutput(
            string name,
            string path,
            string? groupId,
            string status,
            string? projectId)
        {
            Name = name;
            Path = path;
            GroupId = groupId;
            Status = status;
            ProjectId = projectId;
        }

        public string Name { get; }

        public string Path { get; }

        public string? GroupId { get; }

        public string Status { get; set; }

        public string? ProjectId { get; set; }
    }

    private sealed record MachineOutput(
        string Id,
        string Name,
        string Platform,
        string Status,
        DateTimeOffset? LastSeenAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static MachineOutput From(RuntimeMachine machine) =>
            new(
                machine.Id.Value,
                machine.Name,
                machine.Platform,
                machine.Status.ToStorageValue(),
                machine.LastSeenAt,
                machine.CreatedAt,
                machine.UpdatedAt);
    }

    private sealed record TaskOutput(
        string Id,
        string ProjectId,
        string MachineId,
        string Title,
        string Goal,
        string Status,
        int Priority,
        int MaxIterations,
        int CurrentIteration,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        DateTimeOffset? CancelledAt,
        string? FailureReason)
    {
        public static TaskOutput From(RuntimeTask task) =>
            new(
                task.Id.Value,
                task.ProjectId.Value,
                task.MachineId.Value,
                task.Title,
                task.Goal,
                task.Status.ToStorageValue(),
                task.Priority,
                task.MaxIterations,
                task.CurrentIteration,
                task.CreatedAt,
                task.UpdatedAt,
                task.StartedAt,
                task.CompletedAt,
                task.CancelledAt,
                task.FailureReason);
    }

    private sealed record TalkExchangeOutput(
        string SessionId,
        string Source,
        string RunnerId,
        string UserMessageId,
        string AssistantMessageId,
        string Response)
    {
        public static TalkExchangeOutput From(TalkExchange exchange) =>
            new(
                exchange.Session.Id.Value,
                exchange.Session.Source,
                exchange.Session.RunnerId.Value,
                exchange.UserMessage.Id.Value,
                exchange.AssistantMessage.Id.Value,
                exchange.AssistantMessage.Content);
    }

    private sealed record AgentSnapshotOutput(
        string MachineId,
        string? DatabasePath,
        DateTimeOffset HeartbeatAt,
        int QueuedTaskCount,
        string? ClaimedTaskId,
        string? CreatedIterationId,
        string? PromptArtifactId,
        string? PromptPath,
        string? WorktreePath,
        string? ArtifactOutputDirectory,
        string? RunnerExecutionId,
        string? RunnerStatus,
        int? RunnerExitCode,
        string? RunnerErrorSummary,
        bool WorktreeCreated,
        string? WorktreeBaseCommit)
    {
        public static AgentSnapshotOutput From(AgentRunSnapshot snapshot) =>
            new(
                snapshot.MachineId,
                snapshot.DatabasePath,
                snapshot.HeartbeatAt,
                snapshot.QueuedTaskCount,
                snapshot.ClaimedTaskId,
                snapshot.CreatedIterationId,
                snapshot.PromptArtifactId,
                snapshot.PromptPath,
                snapshot.WorktreePath,
                snapshot.ArtifactOutputDirectory,
                snapshot.RunnerExecutionId,
                snapshot.RunnerStatus,
                snapshot.RunnerExitCode,
                snapshot.RunnerErrorSummary,
                snapshot.WorktreeCreated,
                snapshot.WorktreeBaseCommit);
    }

    private sealed record TelegramProcessStatusOutput(
        string Status,
        string MetadataPath,
        int? ProcessId,
        DateTimeOffset? StartedAt,
        string? StdoutPath,
        string? StderrPath)
    {
        public static TelegramProcessStatusOutput From(TelegramProcessStatus status) =>
            new(
                status.IsRunning ? "running" : status.IsStale ? "stale" : "stopped",
                status.MetadataPath,
                status.Metadata?.ProcessId,
                status.Metadata?.StartedAt,
                status.Metadata?.StdoutPath,
                status.Metadata?.StderrPath);
    }

    private sealed record AgentProcessStatusOutput(
        string Status,
        string MetadataPath,
        int? ProcessId,
        DateTimeOffset? StartedAt,
        string? StdoutPath,
        string? StderrPath)
    {
        public static AgentProcessStatusOutput From(AgentProcessStatus status) =>
            new(
                status.IsRunning ? "running" : status.IsStale ? "stale" : "stopped",
                status.MetadataPath,
                status.Metadata?.ProcessId,
                status.Metadata?.StartedAt,
                status.Metadata?.StdoutPath,
                status.Metadata?.StderrPath);
    }

    private sealed record CodexAvailabilityOutput(
        bool IsAvailable,
        string Executable,
        string? ResolvedPath,
        string Message,
        string InstallationUrl)
    {
        public static CodexAvailabilityOutput From(CodexRunnerAvailability availability) =>
            new(
                availability.IsAvailable,
                availability.Executable,
                availability.ResolvedPath,
                availability.Message,
                CodexRunnerAvailability.CodexProjectUrl);
    }

    private sealed record TaskStatusCountOutput(string Status, int Count);

    private sealed record VersionOutput(string Name, string Version);

    private sealed record AegesUpdatePlan(
        string ToolPackage,
        string? TargetVersion,
        string PackageSource,
        string? DownloadedPackagePath);

    private sealed record DotnetToolResult(
        string Command,
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed record WindowsDeferredUpdate(
        string ScriptPath,
        string LogPath,
        bool Elevated);

    private sealed record AegesUpdateResult(
        bool Updated,
        bool DryRun,
        bool UpToDate,
        bool DeferredUpdate,
        bool Elevated,
        string CurrentVersion,
        string? TargetVersion,
        string PackageSource,
        string ToolPackage,
        string? DeferredScriptPath,
        string? DeferredLogPath,
        string? UpdateCommand,
        int? UpdateExitCode,
        string? InstallCommand,
        int? InstallExitCode,
        string? UpdateStandardOutput,
        string? UpdateStandardError,
        string? InstallStandardOutput,
        string? InstallStandardError)
    {
        public static AegesUpdateResult FromDryRun(
            string currentVersion,
            string? targetVersion,
            string packageSource,
            string toolPackage) =>
            new(
                Updated: false,
                DryRun: true,
                UpToDate: false,
                DeferredUpdate: false,
                Elevated: false,
                currentVersion,
                targetVersion,
                packageSource,
                toolPackage,
                DeferredScriptPath: null,
                DeferredLogPath: null,
                UpdateCommand: null,
                UpdateExitCode: null,
                InstallCommand: null,
                InstallExitCode: null,
                UpdateStandardOutput: null,
                UpdateStandardError: null,
                InstallStandardOutput: null,
                InstallStandardError: null);

        public static AegesUpdateResult FromUpToDate(
            string currentVersion,
            string? targetVersion,
            string packageSource,
            string toolPackage) =>
            new(
                Updated: false,
                DryRun: false,
                UpToDate: true,
                DeferredUpdate: false,
                Elevated: false,
                currentVersion,
                targetVersion,
                packageSource,
                toolPackage,
                DeferredScriptPath: null,
                DeferredLogPath: null,
                UpdateCommand: null,
                UpdateExitCode: null,
                InstallCommand: null,
                InstallExitCode: null,
                UpdateStandardOutput: null,
                UpdateStandardError: null,
                InstallStandardOutput: null,
                InstallStandardError: null);

        public static AegesUpdateResult FromDeferred(
            string currentVersion,
            string? targetVersion,
            string packageSource,
            string toolPackage,
            string scriptPath,
            string logPath,
            bool elevated) =>
            new(
                Updated: false,
                DryRun: false,
                UpToDate: false,
                DeferredUpdate: true,
                Elevated: elevated,
                currentVersion,
                targetVersion,
                packageSource,
                toolPackage,
                scriptPath,
                logPath,
                UpdateCommand: null,
                UpdateExitCode: null,
                InstallCommand: null,
                InstallExitCode: null,
                UpdateStandardOutput: null,
                UpdateStandardError: null,
                InstallStandardOutput: null,
                InstallStandardError: null);
    }

    private sealed record LocalRuntimeStatusOutput(
        string Version,
        string RuntimeRootPath,
        string ConfigPath,
        SqliteMigrationStatus Database,
        AgentProcessStatusOutput Agent,
        TelegramProcessStatusOutput Telegram,
        CodexAvailabilityOutput Codex,
        int? ProjectCount,
        int? MachineCount,
        IReadOnlyList<TaskStatusCountOutput> TaskCounts)
    {
        public static LocalRuntimeStatusOutput PendingDatabase(
            RuntimeDirectoryLayout layout,
            string configPath,
            SqliteMigrationStatus database,
            AgentProcessStatus agent,
            TelegramProcessStatus telegram,
            CodexRunnerAvailability codex) =>
            new(
                GetVersion(),
                layout.RootPath,
                configPath,
                database,
                AgentProcessStatusOutput.From(agent),
                TelegramProcessStatusOutput.From(telegram),
                CodexAvailabilityOutput.From(codex),
                null,
                null,
                []);

        public static LocalRuntimeStatusOutput Ready(
            RuntimeDirectoryLayout layout,
            string configPath,
            SqliteMigrationStatus database,
            AgentProcessStatus agent,
            TelegramProcessStatus telegram,
            CodexRunnerAvailability codex,
            int projectCount,
            int machineCount,
            IReadOnlyList<TaskStatusCountOutput> taskCounts) =>
            new(
                GetVersion(),
                layout.RootPath,
                configPath,
                database,
                AgentProcessStatusOutput.From(agent),
                TelegramProcessStatusOutput.From(telegram),
                CodexAvailabilityOutput.From(codex),
                projectCount,
                machineCount,
                taskCounts);
    }

    private sealed record InitResultOutput(
        string RuntimeRootPath,
        string ConfigPath,
        string? DatabasePath,
        bool DatabaseUpToDate,
        ProjectOutput Project,
        bool ProjectCreated,
        MachineOutput Machine,
        bool MachineCreated,
        IReadOnlyList<string> NextSteps);
}
