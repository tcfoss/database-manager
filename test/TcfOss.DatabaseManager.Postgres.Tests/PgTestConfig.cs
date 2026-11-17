using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

namespace TcfOss.DatabaseManager.Postgres.Tests;

public static class PgTestConfig
{
    public static PgConfig GetTestConfig(SqlValueList<Identifier>? searchPath = null, FormattingSettings? formatSettings = null)
    {
        var config = new PgConfig
        {
            ProjectDirectory = ".",
            Version = new Version(18, 0),
            ServerDefaults = new FallbackDatabaseDefaultLoader(NullLogger.Instance).GetServerDefaults(),
            NameHandling = NameHandling.Postgres,
            QuoteStyle = QuoteStyle.Ansi,
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            AttributeDefaults = DefaultSettings.GetAttributeDefaults(),
            NormalizationSettings = DefaultSettings.GetNormalizationDefaults(),
            SearchPath = searchPath ?? [new Identifier("public", QuoteStyle.None)],
            Formatting = formatSettings ?? new FormattingSettings()
        };
        return config;
    }
}
