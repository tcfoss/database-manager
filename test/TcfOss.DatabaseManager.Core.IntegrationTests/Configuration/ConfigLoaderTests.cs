using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.Configuration;

public class ConfigLoaderTests(ConfigLoaderFixture fixture) : IClassFixture<ConfigLoaderFixture>
{
    private ConfigLoaderFixture Fixture { get; } = fixture;

    [Fact]
    public void LoadRawConfig_Works()
    {
        var rawConfig = Fixture.LoadRawConfig();
        Assert.NotNull(rawConfig);
    }

    [Fact]
    public void ConvertToConfig_Works()
    {
        var config = GetConfig();
        Assert.NotNull(config);
    }

    [Fact]
    public void FullConfig_VerifyDeployScripts()
    {
        var config = GetConfig();

        var schemaMap1 = config.Schemas[new SchemaIdentifier("schema1", config.Catalog, config.QuoteStyle)];
        Assert.NotNull(schemaMap1);

        var deployScripts1 = schemaMap1.DeployScripts;
        Assert.NotNull(deployScripts1);

        string filenameBase = Path.Combine(Fixture.RootDirectory.FullName, "Schema1");
        var scripts1UniqueId = "00000000-0000-0000-0000-000000000006";
        var scripts1Type = DeployScriptType.PostDeployment;
        Assert.Equal(3, deployScripts1.Length);
        Assert.Equal("scriptA.sql", deployScripts1[0].FileName);
        Assert.Equal(Path.Combine(filenameBase, "Scripts", "scriptA.sql"), deployScripts1[0].FilePath);
        Assert.Equal(scripts1UniqueId, deployScripts1[0].UniqueId);
        Assert.Equal(scripts1Type, deployScripts1[0].Type);
        Assert.Equal("scriptB.sql", deployScripts1[1].FileName);
        Assert.Equal(Path.Combine(filenameBase, "Scripts", "scriptB.sql"), deployScripts1[1].FilePath);
        Assert.Equal(scripts1UniqueId, deployScripts1[1].UniqueId);
        Assert.Equal(scripts1Type, deployScripts1[1].Type);
        Assert.Equal("scriptC.sql", deployScripts1[2].FileName);
        Assert.Equal(Path.Combine(filenameBase, "Scripts", "scriptC.sql"), deployScripts1[2].FilePath);
        Assert.Equal(scripts1UniqueId, deployScripts1[2].UniqueId);
        Assert.Equal(scripts1Type, deployScripts1[2].Type);


        var schemaMap2 = config.Schemas[new SchemaIdentifier("schema2", config.Catalog, config.QuoteStyle)];
        Assert.NotNull(schemaMap2);

        var deployScripts2 = schemaMap2.DeployScripts;
        Assert.NotNull(deployScripts2);

        filenameBase = Path.Combine(Fixture.RootDirectory.FullName, "Schema2");
        var scripts2Type = DeployScriptType.PreDeployment;
        Assert.Equal(9, deployScripts2.Length);
        Assert.Equal("my_script.sql", deployScripts2[0].FileName);
        Assert.Equal(Path.Combine(filenameBase, "my_script.sql"), deployScripts2[0].FilePath);
        Assert.Equal(scripts2Type, deployScripts2[0].Type);

        Assert.Equal("script1.sql", deployScripts2[1].FileName);
        Assert.Equal(Path.Combine(filenameBase, "Stuff", "script1.sql"), deployScripts2[1].FilePath);
        Assert.Equal(DeployScriptType.PreDeployment, deployScripts2[1].Type);
        Assert.Null(deployScripts2[1].UniqueId);
        Assert.Equal("script2.sql", deployScripts2[2].FileName);
        Assert.Equal(Path.Combine(filenameBase, "Stuff", "script2.sql"), deployScripts2[2].FilePath);
        Assert.Equal(DeployScriptType.PostDropConstraints, deployScripts2[2].Type);
        Assert.Equal("00000000-0000-0000-0000-000000000002", deployScripts2[2].UniqueId);
        Assert.Equal("script3.sql", deployScripts2[3].FileName);
        Assert.Equal(Path.Combine(filenameBase, "Stuff", "script3.sql"), deployScripts2[3].FilePath);
        Assert.Equal(DeployScriptType.PreAddConstraints, deployScripts2[3].Type);
        Assert.Null(deployScripts2[3].UniqueId);
        Assert.Equal("script4.sql", deployScripts2[4].FileName);
        Assert.Equal(Path.Combine(filenameBase, "Stuff", "script4.sql"), deployScripts2[4].FilePath);
        Assert.Equal(DeployScriptType.PostDeployment, deployScripts2[4].Type);
        Assert.Equal("00000000-0000-0000-0000-000000000004", deployScripts2[4].UniqueId);

        // script1 and script3 inherit UniqueId from database-manager.yaml
        // script2 and script4 have their own UniqueId
        Assert.Equal("script1.sql", deployScripts2[5].FileName);
        Assert.Equal(Path.Combine(filenameBase, "MoreStuff", "script1.sql"), deployScripts2[5].FilePath);
        Assert.Equal(DeployScriptType.PreDeployment, deployScripts2[5].Type);
        Assert.Equal("00000000-0000-0000-0000-000000000005", deployScripts2[5].UniqueId);
        Assert.Equal("script2.sql", deployScripts2[6].FileName);
        Assert.Equal(Path.Combine(filenameBase, "MoreStuff", "script2.sql"), deployScripts2[6].FilePath);
        Assert.Equal(DeployScriptType.PostDropConstraints, deployScripts2[6].Type);
        Assert.Equal("00000000-0000-0000-0000-000000000006", deployScripts2[6].UniqueId);
        Assert.Equal("script3.sql", deployScripts2[7].FileName);
        Assert.Equal(Path.Combine(filenameBase, "MoreStuff", "script3.sql"), deployScripts2[7].FilePath);
        Assert.Equal(DeployScriptType.PreAddConstraints, deployScripts2[7].Type);
        Assert.Equal("00000000-0000-0000-0000-000000000005", deployScripts2[7].UniqueId);
        Assert.Equal("script4.sql", deployScripts2[8].FileName);
        Assert.Equal(Path.Combine(filenameBase, "MoreStuff", "script4.sql"), deployScripts2[8].FilePath);
        Assert.Equal(DeployScriptType.PostDeployment, deployScripts2[8].Type);
        Assert.Equal("00000000-0000-0000-0000-000000000008", deployScripts2[8].UniqueId);

        var schemaMap3 = config.Schemas[new SchemaIdentifier("schema3", config.Catalog, config.QuoteStyle)];
        Assert.NotNull(schemaMap3);

        var deployScripts3 = schemaMap3.DeployScripts;
        Assert.NotNull(deployScripts3);
        Assert.Empty(deployScripts3);
    }

    private ConfigGeneric GetConfig(ConfigParsing.Config? rawConfig = null)
    {
        rawConfig ??= Fixture.LoadRawConfig();
        var loader = new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>());
        var config = loader.LoadConfig(Fixture.RootDirectory.FullName, rawConfig, []);
        return config;
    }
}
