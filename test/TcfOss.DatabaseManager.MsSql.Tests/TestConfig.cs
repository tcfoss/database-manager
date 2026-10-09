using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.Tests;

public class TestConfig
{
    public static MsConfig GetMsTestConfig(ValidationSettings? validationsettings = null, FormattingSettings? formatSettings = null)
    {
        var catalog = new CatalogIdentifier("MyDatabase", QuoteStyle.Brackets);

        return new MsConfig()
        {
            Catalog = catalog,
            QuoteStyle = QuoteStyle.Brackets,
            Version = new Version(10, 0, 0),
            ProjectDirectory = "/fake/root/directory",
            DatabaseCredentials = new MsDatabaseCredentials()
            {
                Username = "test_user",
                Password = "test_password",
                Host = "localhost",
                Encrypt = true,
                ConnectionTimeout = 30
            },
            Schemas =
            {
                {
                    new SchemaIdentifier("dbo", catalog, QuoteStyle.Brackets),
                    new MsSchemaMapping()
                    {
                        SchemaName = new SchemaIdentifier("dbo", catalog, QuoteStyle.Brackets),
                        RootPath = "/fake/path/to/dbo",
                    }
                },
                {
                    new SchemaIdentifier("schema2", catalog, QuoteStyle.Brackets),
                    new MsSchemaMapping()
                    {
                        SchemaName = new SchemaIdentifier("idp", catalog, QuoteStyle.Brackets),
                        RootPath = "/fake/path/to/idp",
                    }
                }
            },
            ValidationSettings = validationsettings ?? new ValidationSettings(),
            Formatting = formatSettings ?? new FormattingSettings(),
            DatabaseAvailable = true
        };
    }
}
