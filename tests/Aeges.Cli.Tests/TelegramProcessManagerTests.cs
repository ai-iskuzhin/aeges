using System.Diagnostics;
using System.Text.Json;
using Aeges.Application.Runtime;

namespace Aeges.Cli.Tests;

public sealed class TelegramProcessManagerTests
{
    [Fact]
    public async Task GetStatusAsync_reports_not_running_without_metadata()
    {
        using var runtime = new TemporaryRuntimeDirectory();
        var status = await new TelegramProcessManager(runtime.Layout).GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsRunning);
        Assert.False(status.IsStale);
        Assert.Null(status.Metadata);
        Assert.EndsWith(Path.Combine("runs", "telegram.pid.json"), status.MetadataPath);
    }

    [Fact]
    public async Task GetStatusAsync_reports_running_for_live_metadata_process()
    {
        using var runtime = new TemporaryRuntimeDirectory();
        await runtime.WriteMetadataAsync(Process.GetCurrentProcess().Id);

        var status = await new TelegramProcessManager(runtime.Layout).GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsRunning);
        Assert.False(status.IsStale);
        Assert.Equal(Process.GetCurrentProcess().Id, status.Metadata?.ProcessId);
    }

    [Fact]
    public async Task GetStatusAsync_reports_stale_for_missing_process()
    {
        using var runtime = new TemporaryRuntimeDirectory();
        await runtime.WriteMetadataAsync(int.MaxValue);

        var status = await new TelegramProcessManager(runtime.Layout).GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsRunning);
        Assert.True(status.IsStale);
        Assert.Equal(int.MaxValue, status.Metadata?.ProcessId);
    }

    [Fact]
    public void RuntimeLock_prevents_multiple_telegram_transports()
    {
        using var runtime = new TemporaryRuntimeDirectory();
        using var firstLock = TelegramRuntimeLock.TryAcquire(runtime.Layout);

        using var secondLock = TelegramRuntimeLock.TryAcquire(runtime.Layout);

        Assert.NotNull(firstLock);
        Assert.Null(secondLock);
        Assert.False(TelegramRuntimeLock.CanAcquire(runtime.Layout));
    }

    [Fact]
    public void RuntimeLock_releases_when_disposed()
    {
        using var runtime = new TemporaryRuntimeDirectory();
        using (var firstLock = TelegramRuntimeLock.TryAcquire(runtime.Layout))
        {
            Assert.NotNull(firstLock);
        }

        using var secondLock = TelegramRuntimeLock.TryAcquire(runtime.Layout);

        Assert.NotNull(secondLock);
    }

    private sealed class TemporaryRuntimeDirectory : IDisposable
    {
        public TemporaryRuntimeDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "aeges-cli-runtime", Guid.NewGuid().ToString("N"));
            Layout = RuntimeDirectoryLayout.Create(RootPath);

            foreach (var directory in Layout.RequiredDirectories)
            {
                Directory.CreateDirectory(directory);
            }
        }

        public string RootPath { get; }

        public RuntimeDirectoryLayout Layout { get; }

        public async Task WriteMetadataAsync(int processId)
        {
            var metadata = new TelegramProcessMetadata(
                processId,
                DateTimeOffset.UtcNow,
                "aeges",
                ["telegram", "run", "--no-interactive"],
                Path.Combine(Layout.LogsPath, "telegram.stdout.log"),
                Path.Combine(Layout.LogsPath, "telegram.stderr.log"),
                null,
                null);

            await using var stream = File.Create(Path.Combine(Layout.RunsPath, "telegram.pid.json"));
            await JsonSerializer.SerializeAsync(
                stream,
                metadata,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
