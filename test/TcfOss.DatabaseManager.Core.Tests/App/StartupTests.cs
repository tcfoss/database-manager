using TcfOss.DatabaseManager.Core.App;

namespace TcfOss.DatabaseManager.Core.Tests.App;

public class StartupTests
{
    // --- Explicit config path ---

    [Fact]
    public void GetConfigPath_ExplicitAbsolutePath()
    {
        var configPath = Path.GetFullPath("/my/config.yaml");
        FileInfo? result = StartupBase.GetConfigPath(Environment.CurrentDirectory, configPath);
        Assert.NotNull(result);
        Assert.Equal(configPath, result.FullName);
    }

    [Fact]
    public void GetConfigPath_ExplicitAbsolutePath_IgnoresWorkingDir()
    {
        var configPath = Path.GetFullPath("/other/config.yaml");
        FileInfo? result = StartupBase.GetConfigPath(Path.GetFullPath("/my/project"), configPath);
        Assert.NotNull(result);
        Assert.Equal(configPath, result.FullName);
    }

    [Fact]
    public void GetConfigPath_ExplicitRelativePath_AbsoluteWorkingDir()
    {
        var workingDir = Path.GetFullPath("/my/project");
        FileInfo? result = StartupBase.GetConfigPath(workingDir, "config.yaml");
        Assert.NotNull(result);
        Assert.Equal(Path.Combine(workingDir, "config.yaml"), result.FullName);
    }

    [Fact]
    public void GetConfigPath_ExplicitRelativePath_RelativeWorkingDir()
    {
        FileInfo? result = StartupBase.GetConfigPath("my/project", "config.yaml");
        Assert.NotNull(result);
        Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "my", "project", "config.yaml"), result.FullName);
    }

    // --- Search (no explicit config path) ---

    [Fact]
    public void GetConfigPath_NoConfigPath_ReturnsNullWhenNotFound()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            FileInfo? result = StartupBase.GetConfigPath(tempDir, null);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void GetConfigPath_NoConfigPath_FindsDatabaseManagerYaml()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string configFile = Path.Combine(tempDir, "database-manager.yaml");
        File.WriteAllText(configFile, "");
        try
        {
            FileInfo? result = StartupBase.GetConfigPath(tempDir, null);
            Assert.NotNull(result);
            Assert.Equal(configFile, result.FullName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetConfigPath_NoConfigPath_FindsByDirectoryName()
    {
        // Use a predictable dir name so we can construct the expected filename
        string dirName = $"myproject-{Guid.NewGuid():N}"[..20];
        string tempDir = Path.Combine(Path.GetTempPath(), dirName);
        Directory.CreateDirectory(tempDir);
        string configFile = Path.Combine(tempDir, $"{dirName}.yaml");
        File.WriteAllText(configFile, "");
        try
        {
            FileInfo? result = StartupBase.GetConfigPath(tempDir, null);
            Assert.NotNull(result);
            Assert.Equal(configFile, result.FullName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetConfigPath_NoConfigPath_SearchesParentDirectory()
    {
        string tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string childDir = Path.Combine(tempBase, "subdir");
        Directory.CreateDirectory(childDir);
        string configFile = Path.Combine(tempBase, "database-manager.yaml");
        File.WriteAllText(configFile, "");
        try
        {
            FileInfo? result = StartupBase.GetConfigPath(childDir, null);
            Assert.NotNull(result);
            Assert.Equal(configFile, result.FullName);
        }
        finally
        {
            Directory.Delete(tempBase, recursive: true);
        }
    }
}
