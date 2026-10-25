using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.Configuration;

public class ConfigurationFileIntegrationTests
{
    [Fact]
    public void LoadRawConfig_NullOrMissingFile_ReturnsNull()
    {
        using var fixture = new FsProjectFixture();

        Assert.Null(ConfigurationFile.LoadRawConfig(null));
        Assert.Null(ConfigurationFile.LoadRawConfig(new FileInfo(fixture.CombinePath("missing.yaml"))));
    }

    [Fact]
    public async Task GetConfigPath_AndLoadRawConfig_DiscoverAndParseYaml()
    {
        using var fixture = new FsProjectFixture();
        await fixture.WriteTextAsync("dbman.yml", "dialect: generic\ncatalog: example\n", TestContext.Current.CancellationToken);

        FileInfo? configFile = ConfigurationFile.GetConfigPath(fixture.RootDirectory.FullName, null);
        var config = ConfigurationFile.LoadRawConfig(configFile);

        Assert.NotNull(config);
        Assert.Equal(SqlDialect.Generic, config.Dialect);
        Assert.Equal("example", config.Catalog);
    }

    [Fact]
    public async Task GetConfigPath_MultipleCandidates_Throws()
    {
        using var fixture = new FsProjectFixture();
        await fixture.WriteTextAsync("dbman.yml", "dialect: generic\n", TestContext.Current.CancellationToken);
        await fixture.WriteTextAsync("database-manager.yaml", "dialect: generic\n", TestContext.Current.CancellationToken);

        Assert.Throws<ConfigurationException.AmbiguousConfigFile>(() =>
            ConfigurationFile.GetConfigPath(fixture.RootDirectory.FullName, null));
    }

    [Fact]
    public void GetConfigPath_NoConfigPath_ReturnsNullWhenNotFound()
    {
        using var fixture = new FsProjectFixture();

        Assert.Null(ConfigurationFile.GetConfigPath(fixture.RootDirectory.FullName, null));
    }

    [Fact]
    public async Task GetConfigPath_NoConfigPath_FindsDatabaseManagerYaml()
    {
        using var fixture = new FsProjectFixture();
        await fixture.WriteTextAsync("database-manager.yaml", "", TestContext.Current.CancellationToken);

        FileInfo? result = ConfigurationFile.GetConfigPath(fixture.RootDirectory.FullName, null);

        Assert.NotNull(result);
        Assert.Equal(fixture.CombinePath("database-manager.yaml"), result.FullName);
    }

    [Fact]
    public async Task GetConfigPath_NoConfigPath_FindsByDirectoryName()
    {
        using var fixture = new FsProjectFixture();
        string directoryName = fixture.RootDirectory.Name;
        string fileName = $"{directoryName}.yaml";
        await fixture.WriteTextAsync(fileName, "", TestContext.Current.CancellationToken);

        FileInfo? result = ConfigurationFile.GetConfigPath(fixture.RootDirectory.FullName, null);

        Assert.NotNull(result);
        Assert.Equal(fixture.CombinePath(fileName), result.FullName);
    }

    [Fact]
    public async Task GetConfigPath_NoConfigPath_SearchesParentDirectory()
    {
        using var fixture = new FsProjectFixture();
        await fixture.WriteTextAsync("database-manager.yaml", "", TestContext.Current.CancellationToken);
        string childDirectory = fixture.CombinePath("subdir");
        Directory.CreateDirectory(childDirectory);

        FileInfo? result = ConfigurationFile.GetConfigPath(childDirectory, null);

        Assert.NotNull(result);
        Assert.Equal(fixture.CombinePath("database-manager.yaml"), result.FullName);
    }
}
