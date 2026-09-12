using TcfOss.DatabaseManager.Core.Configuration.Attributes;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.Configuration;

public static class TestDataRawConfig
{
    public static ConfigParsing.Config MsTestRawConfig => new()
    {
        Catalog = "mydb",
        Dialect = SqlDialect.MsSql,
        Schemas =
        [
            new ConfigParsing.SchemaMapping
            {
                SchemaName = "dbo",
                RootPath = "dbo",
                ExcludeDatabaseObjectNames = ["excluded_table"],
                ExcludeFilePatterns = ["**/*badfile.sql"]
            },
            new ConfigParsing.SchemaMapping
            {
                SchemaName = "reporting",
                RootPath = "subdir/reporting",
                ExcludeDatabaseObjectNames = [],
                ExcludeFilePatterns = []
            }
        ],
        Credentials = new ConfigParsing.Credentials
        {
            Hostname = "sqlserver",
            Port = "1433",
            Username = "testuser",
            Password = "testpassword"
        },
    };

    public static ConfigParsing.Config MsTestRawConfigWindowsAuth => new()
    {
        Catalog = "mydb",
        Dialect = SqlDialect.MsSql,
        Schemas =
        [
            new ConfigParsing.SchemaMapping
            {
                SchemaName = "dbo",
                RootPath = "dbo",
            }
        ],
        Credentials = new ConfigParsing.Credentials
        {
            Hostname = "sqlserver",
        },
    };
}
