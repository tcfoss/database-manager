using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Postgres.Configuration;

namespace TcfOss.DatabaseManager.Postgres.Tests;

public static class PgTestConfig
{
    public static PgConfig GetTestConfig(SqlValueList<Identifier>? searchPath = null, FormattingSettings? formatSettings = null)
    {
        var config = new PgConfig
        {
            ProjectDirectory = ".",
            QuoteStyle = QuoteStyle.Ansi,
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            SearchPath = searchPath ?? [new Identifier("public", QuoteStyle.None)],
            Formatting = formatSettings ?? new FormattingSettings(),
            NameHandling = NameHandling.LowercaseUnlessQuoted
        };
        return config;
    }
}
