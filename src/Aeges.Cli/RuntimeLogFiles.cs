using System.Globalization;

/// <summary>
/// Provides bounded append-only log files for local background process output.
/// </summary>
internal static class RuntimeLogFiles
{
    /// <summary>
    /// Default maximum size for a single runtime log file.
    /// </summary>
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Default number of rotated log files to retain beside the active file.
    /// </summary>
    public const int DefaultRetainedFiles = 5;

    /// <summary>
    /// Appends a log line after rotating the file when it exceeds the configured size.
    /// </summary>
    /// <param name="path">The active log file path.</param>
    /// <param name="line">The line to append.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the line is written.</returns>
    public static async Task AppendLineAsync(
        string path,
        string line,
        CancellationToken cancellationToken) =>
        await AppendLineAsync(path, line, DefaultMaxBytes, DefaultRetainedFiles, cancellationToken);

    /// <summary>
    /// Appends a log line after rotating the file when it exceeds the configured size.
    /// </summary>
    /// <param name="path">The active log file path.</param>
    /// <param name="line">The line to append.</param>
    /// <param name="maxBytes">The maximum active file size before rotation.</param>
    /// <param name="retainedFiles">The number of rotated files to retain.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the line is written.</returns>
    public static async Task AppendLineAsync(
        string path,
        string line,
        long maxBytes,
        int retainedFiles,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        RotateIfNeeded(path, maxBytes, retainedFiles);
        await File.AppendAllTextAsync(path, $"{line}{Environment.NewLine}", cancellationToken);
    }

    /// <summary>
    /// Rotates the active log file when it exceeds the configured size.
    /// </summary>
    /// <param name="path">The active log file path.</param>
    /// <param name="maxBytes">The maximum active file size before rotation.</param>
    /// <param name="retainedFiles">The number of rotated files to retain.</param>
    public static void RotateIfNeeded(string path, long maxBytes, int retainedFiles)
    {
        if (maxBytes <= 0 || retainedFiles <= 0 || !File.Exists(path))
        {
            return;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length < maxBytes)
        {
            return;
        }

        var oldestPath = GetRotatedPath(path, retainedFiles);
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }

        for (var index = retainedFiles - 1; index >= 1; index--)
        {
            var source = GetRotatedPath(path, index);
            if (File.Exists(source))
            {
                File.Move(source, GetRotatedPath(path, index + 1));
            }
        }

        File.Move(path, GetRotatedPath(path, 1));
    }

    /// <summary>
    /// Builds a stable rotated log path from an active log path and rotation index.
    /// </summary>
    /// <param name="path">The active log file path.</param>
    /// <param name="index">The one-based rotation index.</param>
    /// <returns>The rotated log file path.</returns>
    public static string GetRotatedPath(string path, int index) =>
        $"{path}.{index.ToString(CultureInfo.InvariantCulture)}";
}
