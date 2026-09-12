using TcfOss.DatabaseManager.Core.Configuration.Attributes;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.Configuration;

public static class TestDataRawConfig
{
    public static ConfigParsing.Config MyTestRawConfig => GetMyTestRawConfig();

    public static ConfigParsing.Config GetMyTestRawConfig(
        string? projectDirectory = null,
        SqlDialect? dialect = null,
        ConfigParsing.AttributeDefaults? attributeDefaults = null,
        ConfigParsing.DifferFormattingSettings? differFormattingSettings = null)
    {
        var config = new ConfigParsing.Config
        {
            Catalog = "mycatalog",
            Dialect = dialect ?? SqlDialect.MySql,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "schema1",
                    RootPath = "schema_1",
                    ExcludeDatabaseObjectNames = ["excluded_table"],
                    ExcludeFilePatterns = ["**/*badfile.sql"]
                },
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "schema2",
                    RootPath = "subdir/schema_2",
                    ExcludeDatabaseObjectNames = [],
                    ExcludeFilePatterns = []
                }
            ],
            Credentials = new ConfigParsing.Credentials
            {
                Hostname = "localhost",
                Port = "3306",
                Username = "testuser",
                Password = "testpassword"
            },
            AttributeDefaults = attributeDefaults,
            DifferFormatting = differFormattingSettings,
        };
        if (projectDirectory != null)
        {
            config.ProjectDirectory = projectDirectory;
        }
        return config;
    }

    public static ConfigParsing.Config MyTestRawConfigSillyDefaults => new()
    {
        Dialect = SqlDialect.MySql,
        Schemas =
        [
            new ConfigParsing.SchemaMapping
            {
                SchemaName = "schema1",
                RootPath = "schema_1",
                ExcludeDatabaseObjectNames = ["excluded_table"],
                ExcludeFilePatterns = ["**/*badfile.sql"]
            },
            new ConfigParsing.SchemaMapping
            {
                SchemaName = "schema2",
                RootPath = "subdir/schema_2",
                ExcludeDatabaseObjectNames = [],
                ExcludeFilePatterns = []
            }
        ],
        Credentials = new ConfigParsing.Credentials
        {
            Hostname = "localhost",
            Port = "3306",
            Username = "testuser",
            Password = "testpassword"
        },
        AttributeDefaults = new ConfigParsing.AttributeDefaults
        {
            NumericAttribute = ConfigParsing.Attributes.MySqlNumericAttribute.Unsigned,
            UnsignedBigIntWidth = 25,
            SignedIntWidth = 3,
            IndexMethod = ConfigParsing.Attributes.IndexMethod.Hash,
        },
    };
}
