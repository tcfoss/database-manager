using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.Configuration;

public class ConfigLoaderTests
{
    [Fact]
    public void Test_Basic_Config_Loads()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        var config = new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>()).LoadConfig("/home/username/database", rawConfig, []);

        // Validate the loaded config
        Assert.NotNull(config);

        Assert.Equal("mycatalog", config.Catalog.Name);
        Assert.Equal(QuoteStyle.Backticks, config.QuoteStyle);
        Assert.Equal("/fake/project/root", config.ProjectDirectory);
        Assert.Equal(2, config.Schemas.Count);

        // Validate schema mappings
        var schemaValues = config.Schemas.Values.ToList();
        var schema1 = schemaValues[0];
        var schema2 = schemaValues[1];
        Assert.Equal("schema1", schema1.SchemaName.Name);
        Assert.Contains("excluded_table", schema1.ExcludeDatabaseObjectNames);

        // Validate schema paths are absolute
        Assert.True(Path.IsPathRooted(config.ProjectDirectory));
        Assert.True(Path.IsPathRooted(schema1.RootPath));
        Assert.True(Path.IsPathRooted(schema2.RootPath));
        var osRoot = Path.GetFullPath("/fake/project/root");
        Assert.StartsWith(osRoot, schema1.RootPath);
        Assert.StartsWith(Path.Combine(osRoot, "subdir"), schema2.RootPath);
    }

    [Fact]
    public void TestFillsDefaults()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfigNoOptionalStuff;
        var configPath = Path.GetFullPath("/home/username/database");
        var config = new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>()).LoadConfig(configPath, rawConfig, []);

        // Validate defaults
        Assert.Equal(QuoteStyle.Ansi, config.QuoteStyle);
        Assert.Equal(new CatalogIdentifier("def", QuoteStyle.Ansi), config.Catalog);
        Assert.Equal(configPath, config.ProjectDirectory);
        var schemaValues = config.Schemas.Values.ToList();
        Assert.StartsWith(Path.Combine(configPath, "schema_1"), schemaValues[0].RootPath);
        Assert.StartsWith(Path.Combine(configPath, "subdir", "schema_2"), schemaValues[1].RootPath);
    }

    [Fact]
    public void Test_Absolutizes_Relative_Project_Directory()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            ProjectDirectory = "relative/path/to/project",
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "test_schema",
                    RootPath = "test_schema_path"
                }
            ]
        };
        var config = new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>()).LoadConfig("/home/username/database", rawConfig, []);

        Assert.Equal(Path.GetFullPath("/home/username/database/relative/path/to/project"), config.ProjectDirectory);
        Assert.StartsWith(Path.GetFullPath("/home/username/database/relative/path/to/project/test_schema_path"), config.Schemas.Values.First().RootPath);
    }

    [Fact]
    public void SchemaRootPath_Empty_Throws()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "test_schema",
                    RootPath = ""
                }
            ]
        };
        Assert.Throws<ConfigurationException.MissingFieldException>(() => new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>()).LoadConfig("/home/username/database", rawConfig, []));
    }

    [Fact]
    public void SchemaName_Empty_Throws()
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "",
                    RootPath = "some/path"
                }
            ]
        };
        Assert.Throws<ConfigurationException.MissingFieldException>(() => new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>()).LoadConfig("/home/username/database", rawConfig, []));
    }

    [Fact]
    public void DifferFormatting_UseDelimiterAroundViews_DefaultsToFalse()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfigNoOptionalStuff;
        var config = new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>()).LoadConfig("/home/username/database", rawConfig, []);

        Assert.False(config.DifferFormatting.UseDelimiterAroundViews);
    }
}
