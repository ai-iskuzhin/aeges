using Aeges.Application.Runtime;

namespace Aeges.Application.Tests;

public sealed class RuntimeDirectoryLayoutTests
{
    [Fact]
    public void Create_builds_expected_runtime_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "aeges-runtime-layout");

        var layout = RuntimeDirectoryLayout.Create(root);

        Assert.Equal(root, layout.RootPath);
        Assert.Equal(Path.Combine(root, "aeges.db"), layout.DatabasePath);
        Assert.Equal(Path.Combine(root, "logs"), layout.LogsPath);
        Assert.Equal(Path.Combine(root, "runs"), layout.RunsPath);
        Assert.Equal(Path.Combine(root, "worktrees"), layout.WorktreesPath);
        Assert.Equal(Path.Combine(root, "artifacts"), layout.ArtifactsPath);
        Assert.Equal(Path.Combine(root, "secrets"), layout.SecretsPath);
        Assert.Equal(Path.Combine(root, "config.json"), layout.ConfigPath);
        Assert.Equal(
            [
                root,
                Path.Combine(root, "logs"),
                Path.Combine(root, "runs"),
                Path.Combine(root, "worktrees"),
                Path.Combine(root, "artifacts"),
                Path.Combine(root, "secrets"),
            ],
            layout.RequiredDirectories);
    }

    [Fact]
    public void Create_normalizes_trailing_directory_separator()
    {
        var root = Path.Combine(Path.GetTempPath(), "aeges-runtime-layout");

        var layout = RuntimeDirectoryLayout.Create(root + Path.DirectorySeparatorChar);

        Assert.Equal(root, layout.RootPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/.aeges")]
    public void Create_rejects_invalid_root_paths(string rootPath)
    {
        Assert.Throws<ArgumentException>(() => RuntimeDirectoryLayout.Create(rootPath));
    }

    [Fact]
    public void CreateDefault_uses_user_home_aeges_directory()
    {
        var layout = RuntimeDirectoryLayout.CreateDefault();
        var expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aeges");

        Assert.Equal(expectedRoot, layout.RootPath);
        Assert.True(Path.IsPathFullyQualified(layout.RootPath));
    }
}
