using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.Configuration;

public static class TestDataRawConfig
{
    public static ConfigParsing.Config MyTestRawConfig => new()
    {
        Catalog = "mycatalog",
        Dialect = SqlDialect.Generic,
        QuoteStyle = QuoteStyle.Backticks,
        ProjectDirectory = "/fake/project/root",
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
        ]
    };

    public static ConfigParsing.Config MyTestRawConfigNoOptionalStuff => new()
    {
        Dialect = SqlDialect.Generic,
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
        ]
    };

    public static ConfigParsing.Config GetMyTestRawConfigLibrarySchemas(ConfigParsing.NormalizationSettings? normalizationSettings = null) => new()
    {
        Catalog = "def",
        Dialect = SqlDialect.Generic,
        QuoteStyle = QuoteStyle.Backticks,
        ProjectDirectory = "/fake/project/root",
        Schemas =
        [
            new ConfigParsing.SchemaMapping
            {
                SchemaName = "library_catalog",
                RootPath = "catalog",
            },
            new ConfigParsing.SchemaMapping
            {
                SchemaName = "library_activity",
                RootPath = "activity",
            },
        ],
        ViewNormalizationSettings = normalizationSettings ?? new ConfigParsing.NormalizationSettings()
    };
}
