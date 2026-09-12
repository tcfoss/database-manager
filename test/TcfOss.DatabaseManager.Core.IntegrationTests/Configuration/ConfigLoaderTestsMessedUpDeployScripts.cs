using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Errors;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.Configuration;

public class ConfigLoaderTestsMessedUpDeployScripts(ConfigLoaderFixture fixture) : IClassFixture<ConfigLoaderFixture>
{
    private ConfigLoaderFixture Fixture { get; } = fixture;

    [Fact]
    public void FullConfig_DeployScriptMissingType_Throws()
    {
        var rawConfig = Fixture.LoadRawConfig();

        var moreScriptsPath = Path.Combine(Fixture.RootDirectory.FullName, "Schema2", "more-scripts.yaml");
        var allLines = File.ReadAllLines(moreScriptsPath).ToList();
        var lineIndex = allLines.FindIndex(l => l.Contains("MoreStuff/script2.sql"));
        allLines.RemoveAt(lineIndex + 1); // Remove the Type line
        File.WriteAllLines(moreScriptsPath, allLines);

        var exception = Assert.Throws<ConfigurationException.DeployScriptMissingType>(() =>
        {
            _ = GetConfig(rawConfig);
        });

        var expectedPath = Path.Combine(Fixture.RootDirectory.FullName, "Schema2", "MoreStuff", "script2.sql");
        var expectedMessage = $$"""Configuration Error: The Type field on DeployScript entry DeployScript { Type = 0, FilePath = {{expectedPath}}, FileName = script2.sql, UniqueId = 00000000-0000-0000-0000-000000000006 } is missing or invalid. It should be one of { PreDeployment | PostDropConstraints | PreAddConstraints | PostDeployment }.""";
        Assert.Equal(expectedMessage, exception.Message);
    }

    private ConfigGeneric GetConfig(ConfigParsing.Config? rawConfig = null)
    {
        rawConfig ??= Fixture.LoadRawConfig();
        var loader = new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>());
        var config = loader.LoadConfig(Fixture.RootDirectory.FullName, rawConfig, []);
        return config;
    }
}
