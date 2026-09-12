using DotNet.Testcontainers.Builders;
using TcfOss.DatabaseManager.Core.IO;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.Configuration;

// ReSharper disable ClassNeverInstantiated.Global
public class ConfigLoaderFixture : FsProjectFixture
{
    public ConfigLoaderFixture()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "ConfigLoadingTests"));
        CopyFiles(sourceDirectory);
    }

    public ConfigParsing.Config LoadRawConfig()
    {
        var configFilePath = Path.Combine(RootDirectory.FullName, "database-manager.yaml");
        var rawConfig = Serialization.ParseConfig<ConfigParsing.Config>(configFilePath);
        return rawConfig;
    }
}
