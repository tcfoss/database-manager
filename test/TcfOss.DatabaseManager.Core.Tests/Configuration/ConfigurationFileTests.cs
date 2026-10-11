using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.Tests.Configuration;

public class ConfigurationFileTests
{
    [Fact]
    public void LoadRawConfig_NullOrMissingFile_ReturnsNull()
    {
        Assert.Null(ConfigurationFile.LoadRawConfig(null));
        Assert.Null(ConfigurationFile.LoadRawConfig(new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.yaml"))));
    }

    [Fact]
    public void GetConfigPath_AndLoadRawConfig_DiscoverAndParseYaml()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-config-tests-");
        try
        {
            string filePath = Path.Combine(directory.FullName, "dbman.yml");
            File.WriteAllText(filePath, "dialect: generic\ncatalog: example\n");

            FileInfo? configFile = ConfigurationFile.GetConfigPath(directory.FullName, null);
            var config = ConfigurationFile.LoadRawConfig(configFile);

            Assert.NotNull(config);
            Assert.Equal(SqlDialect.Generic, config.Dialect);
            Assert.Equal("example", config.Catalog);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetConfigPath_MultipleCandidates_Throws()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-config-tests-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "dbman.yml"), "dialect: generic\n");
            File.WriteAllText(Path.Combine(directory.FullName, "database-manager.yaml"), "dialect: generic\n");

            Assert.Throws<ConfigurationException.AmbiguousConfigFile>(() => ConfigurationFile.GetConfigPath(directory.FullName, null));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

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

    // --- Search (no explicit config path) ---

    [Fact]
    public void GetConfigPath_NoConfigPath_ReturnsNullWhenNotFound()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            FileInfo? result = ConfigurationFile.GetConfigPath(tempDir, null);
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
            FileInfo? result = ConfigurationFile.GetConfigPath(tempDir, null);
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
            FileInfo? result = ConfigurationFile.GetConfigPath(tempDir, null);
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
            FileInfo? result = ConfigurationFile.GetConfigPath(childDir, null);
            Assert.NotNull(result);
            Assert.Equal(configFile, result.FullName);
        }
        finally
        {
            Directory.Delete(tempBase, recursive: true);
        }
    }
}
