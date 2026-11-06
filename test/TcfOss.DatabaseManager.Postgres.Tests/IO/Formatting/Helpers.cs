using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting;

public static class Helpers
{
    public static Formatter CreateFormatter(PseudoTableSet? pseudoTables = null, FormattingSettings? formatSettings = null)
    {
        var config = new PgConfig
        {
            ProjectDirectory = ".",
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            QuoteStyle = QuoteStyle.Ansi,
            Formatting = formatSettings ?? new FormattingSettings()
        };
        return new Formatter(config, new TextParser(new PgLexer(), new PgParser()), new FunctionNameProvider(), pseudoTables?.CloneExternalOnly());
    }

    public static FormatManager CreateFormatManager()
    {
        var functionNameProvider = new FunctionNameProvider();
        return new FormatManager
        {
            Formatting = new FormattingSettings(),
            FunctionNameProvider = functionNameProvider,
            ComponentNormalizer = new ComponentNormalizer(QuoteStyle.Ansi, functionNameProvider),
            Indenter = new Indenter()
        };
    }
}
