using System.Text;
using Aeges.Application.Runtime;

internal sealed class TelegramRuntimeLock : IDisposable
{
    private readonly string lockPath;
    private FileStream? stream;

    private TelegramRuntimeLock(string lockPath, FileStream stream)
    {
        this.lockPath = lockPath;
        this.stream = stream;
    }

    public static TelegramRuntimeLock? TryAcquire(RuntimeDirectoryLayout? layout = null)
    {
        var runtimeLayout = layout ?? RuntimeDirectoryLayout.CreateDefault();
        Directory.CreateDirectory(runtimeLayout.RunsPath);
        var lockPath = GetLockPath(runtimeLayout);

        try
        {
            var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            stream.SetLength(0);
            var content = Encoding.UTF8.GetBytes($"{Environment.ProcessId}{Environment.NewLine}");
            stream.Write(content);
            stream.Flush();

            return new TelegramRuntimeLock(lockPath, stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool CanAcquire(RuntimeDirectoryLayout? layout = null)
    {
        using var runtimeLock = TryAcquire(layout);

        return runtimeLock is not null;
    }

    public void Dispose()
    {
        stream?.Dispose();
        stream = null;

        try
        {
            File.Delete(lockPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetLockPath(RuntimeDirectoryLayout layout) =>
        Path.Combine(layout.RunsPath, "telegram.lock");
}
