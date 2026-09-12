using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.Tests;

public static class TestConfig
{
    private static readonly Dictionary<string, CharacterSetSpec> s_defaultCharacterSets = new()
    {
        {
            "utf8mb4",
            new CharacterSetSpec
            {
                CharacterSet = "utf8mb4",
                DefaultCollation = "utf8mb4_0900_ai_ci",
                Collations = ["utf8mb4_0900_ai_ci", "utf8mb4_general_ci"]
            }
        },
        {
            "latin1",
            new CharacterSetSpec
            {
                CharacterSet = "latin1",
                DefaultCollation = "latin1_swedish_ci",
                Collations = ["latin1_swedish_ci", "latin1_general_ci"]
            }
        }
    };

    private static readonly SchemaDefaults s_defaultSchemaDefaults = new()
    {
        CharacterSet = "utf8mb4",
        Collation = "utf8mb4_0900_ai_ci",
        Engine = "InnoDB"
    };

    public static readonly MyConfig MyTestConfig = new()
    {
        Catalog = new CatalogIdentifier("def", QuoteStyle.Backticks),
        QuoteStyle = QuoteStyle.Backticks,
        Version = new Version(8, 4, 6),
        ProjectDirectory = "/fake/root/directory",
        Schemas =
        {
            {
                new SchemaIdentifier("schema1", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                new MySchemaMapping
                {
                    SchemaName = new SchemaIdentifier("schema1", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                    RootPath = "/fake/path/to/schema1",
                    IncludeFilePatterns = ["**/*.sql"],
                    ExcludeFilePatterns = ["**/*badfile.sql"],
                    ExcludeDatabaseObjectNames = ["excluded_table"],
                    Refactors = [],
                    SchemaDefaults = s_defaultSchemaDefaults
                }
            },
            {
                new SchemaIdentifier("schema2", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                new MySchemaMapping
                {
                    SchemaName = new SchemaIdentifier("schema2", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                    RootPath = "/fake/path/to/schema2",
                    IncludeFilePatterns = ["**/*.sql"],
                    ExcludeFilePatterns = [],
                    ExcludeDatabaseObjectNames = [],
                    Refactors = [],
                    SchemaDefaults = s_defaultSchemaDefaults
                }
            }
        },
        DatabaseCredentials = new DatabaseCredentials
        {
            Host = "localhost",
            Username = "testuser",
            Password = "testpassword",
            Port = 3306,
            ConnectionTimeout = 30
        },
        ValidationSettings = new ValidationSettings(),
        ServerDefaults = s_defaultSchemaDefaults,
        CharacterSets = s_defaultCharacterSets,
        DatabaseAvailable = true
    };

    public static MyConfig GetMyTestConfig(ValidationSettings? validationSettings = null)
    {
        return new MyConfig()
        {
            Catalog = new CatalogIdentifier("def", QuoteStyle.Backticks),
            QuoteStyle = QuoteStyle.Backticks,
            Version = new Version(8, 4, 6),
            ProjectDirectory = "/fake/root/directory",
            Schemas =
            {
                {
                    new SchemaIdentifier("schema1", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                    new MySchemaMapping
                    {
                        SchemaName = new SchemaIdentifier("schema1", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                        RootPath = "/fake/path/to/schema1",
                        IncludeFilePatterns = ["**/*.sql"],
                        ExcludeFilePatterns = ["**/*badfile.sql"],
                        ExcludeDatabaseObjectNames = ["excluded_table"],
                        Refactors = [],
                        SchemaDefaults = s_defaultSchemaDefaults
                    }
                },
                {
                    new SchemaIdentifier("schema2", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                    new MySchemaMapping
                    {
                        SchemaName = new SchemaIdentifier("schema2", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks),
                        RootPath = "/fake/path/to/schema2",
                        IncludeFilePatterns = ["**/*.sql"],
                        ExcludeFilePatterns = [],
                        ExcludeDatabaseObjectNames = [],
                        Refactors = [],
                        SchemaDefaults = s_defaultSchemaDefaults
                    }
                }
            },
            DatabaseCredentials = new DatabaseCredentials
            {
                Host = "localhost",
                Username = "testuser",
                Password = "testpassword",
                Port = 3306,
                ConnectionTimeout = 30
            },
            ValidationSettings = validationSettings ?? new ValidationSettings(),
            ServerDefaults = s_defaultSchemaDefaults,
            CharacterSets = s_defaultCharacterSets,
            DatabaseAvailable = true
        };
    }
}
