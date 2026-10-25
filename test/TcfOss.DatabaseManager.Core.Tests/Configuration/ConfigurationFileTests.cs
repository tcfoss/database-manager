using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Core.Tests.Configuration;

public class ConfigurationFileTests
{
    // --- Explicit config path ---

    [Fact]
    public void GetConfigPath_ExplicitAbsolutePath()
    {
        var configPath = Path.GetFullPath("/my/config.yaml");
        FileInfo? result = ConfigurationFile.GetConfigPath(Environment.CurrentDirectory, configPath);
        Assert.NotNull(result);
        Assert.Equal(configPath, result.FullName);
    }

    [Fact]
    public void GetConfigPath_ExplicitAbsolutePath_IgnoresWorkingDir()
    {
        var configPath = Path.GetFullPath("/other/config.yaml");
        FileInfo? result = ConfigurationFile.GetConfigPath(Path.GetFullPath("/my/project"), configPath);
        Assert.NotNull(result);
        Assert.Equal(configPath, result.FullName);
    }

    [Fact]
    public void GetConfigPath_ExplicitRelativePath_AbsoluteWorkingDir()
    {
        var workingDir = Path.GetFullPath("/my/project");
        FileInfo? result = ConfigurationFile.GetConfigPath(workingDir, "config.yaml");
        Assert.NotNull(result);
        Assert.Equal(Path.Combine(workingDir, "config.yaml"), result.FullName);
    }

    [Fact]
    public void GetConfigPath_ExplicitRelativePath_RelativeWorkingDir()
    {
        FileInfo? result = ConfigurationFile.GetConfigPath("my/project", "config.yaml");
        Assert.NotNull(result);
        Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "my", "project", "config.yaml"), result.FullName);
    }

}
