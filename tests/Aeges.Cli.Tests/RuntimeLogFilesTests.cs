namespace Aeges.Cli.Tests;

public sealed class RuntimeLogFilesTests
{
    [Fact]
    public async Task AppendLineAsync_rotates_active_log_and_retains_bounded_history()
    {
        using var directory = new TemporaryDirectory();
        var logPath = Path.Combine(directory.Path, "telegram.stdout.log");
        await File.WriteAllTextAsync(logPath, new string('a', 10));
        await File.WriteAllTextAsync(RuntimeLogFiles.GetRotatedPath(logPath, 1), "older-1");
        await File.WriteAllTextAsync(RuntimeLogFiles.GetRotatedPath(logPath, 2), "older-2");

        await RuntimeLogFiles.AppendLineAsync(
            logPath,
            "new active line",
            maxBytes: 5,
            retainedFiles: 2,
            CancellationToken.None);

        Assert.Equal($"new active line{Environment.NewLine}", await File.ReadAllTextAsync(logPath));
        Assert.Equal(new string('a', 10), await File.ReadAllTextAsync(RuntimeLogFiles.GetRotatedPath(logPath, 1)));
        Assert.Equal("older-1", await File.ReadAllTextAsync(RuntimeLogFiles.GetRotatedPath(logPath, 2)));
        Assert.False(File.Exists(RuntimeLogFiles.GetRotatedPath(logPath, 3)));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aeges-log-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
