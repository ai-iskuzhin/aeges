namespace Aeges.Application.Runtime;

/// <summary>
/// Describes the local Aeges runtime directory layout.
/// </summary>
public sealed class RuntimeDirectoryLayout
{
    private const string DefaultDirectoryName = ".aeges";

    private RuntimeDirectoryLayout(string rootPath)
    {
        RootPath = RequireAbsoluteRoot(rootPath);
        DatabasePath = Path.Combine(RootPath, "aeges.db");
        LogsPath = Path.Combine(RootPath, "logs");
        RunsPath = Path.Combine(RootPath, "runs");
        WorktreesPath = Path.Combine(RootPath, "worktrees");
        ArtifactsPath = Path.Combine(RootPath, "artifacts");
        SecretsPath = Path.Combine(RootPath, "secrets");
        ConfigPath = Path.Combine(RootPath, "config.json");
        RequiredDirectories =
        [
            RootPath,
            LogsPath,
            RunsPath,
            WorktreesPath,
            ArtifactsPath,
            SecretsPath,
        ];
    }

    /// <summary>
    /// Gets the runtime root path.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets the SQLite database path.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Gets the runtime logs directory path.
    /// </summary>
    public string LogsPath { get; }

    /// <summary>
    /// Gets the run metadata directory path.
    /// </summary>
    public string RunsPath { get; }

    /// <summary>
    /// Gets the Git worktrees directory path.
    /// </summary>
    public string WorktreesPath { get; }

    /// <summary>
    /// Gets the artifact root directory path.
    /// </summary>
    public string ArtifactsPath { get; }

    /// <summary>
    /// Gets the local secret file directory path.
    /// </summary>
    public string SecretsPath { get; }

    /// <summary>
    /// Gets the local runtime configuration file path.
    /// </summary>
    public string ConfigPath { get; }

    /// <summary>
    /// Gets the directories that must exist before local runtime execution.
    /// </summary>
    public IReadOnlyList<string> RequiredDirectories { get; }

    /// <summary>
    /// Creates a runtime directory layout for an explicit root path.
    /// </summary>
    /// <param name="rootPath">The runtime root path.</param>
    /// <returns>The runtime directory layout.</returns>
    public static RuntimeDirectoryLayout Create(string rootPath) => new(rootPath);

    /// <summary>
    /// Creates the default local runtime directory layout under the current user's home directory.
    /// </summary>
    /// <returns>The default runtime directory layout.</returns>
    public static RuntimeDirectoryLayout CreateDefault()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(homePath))
        {
            throw new InvalidOperationException("Unable to resolve the current user's home directory.");
        }

        return new RuntimeDirectoryLayout(Path.Combine(homePath, DefaultDirectoryName));
    }

    private static string RequireAbsoluteRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Runtime root path must not be empty.", nameof(rootPath));
        }

        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw new ArgumentException("Runtime root path must be absolute.", nameof(rootPath));
        }

        return Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
