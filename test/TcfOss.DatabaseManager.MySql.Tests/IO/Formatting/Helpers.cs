using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public static class Helpers
{
    public static readonly PseudoTableSet s_pseudoTables1;
    public static readonly PseudoTableSet s_pseudoTables2;
    public static readonly PseudoTableSet s_pseudoTables3;

    static Helpers()
    {
        var schema = new SchemaIdentifier("schema1", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks);
        s_pseudoTables1 = new PseudoTableSet(schema, [
            new PseudoTable("books", new ObjectIdentifier("books", schema), ["title", "author_id", "published_year"], PseudoTableType.Table),
            new PseudoTable("authors", new ObjectIdentifier("authors", schema), ["id", "name"], PseudoTableType.Table),
        ]);

        s_pseudoTables2 = new PseudoTableSet(schema, [
            new PseudoTable("books", new ObjectIdentifier("books", schema), ["book_id", "title", "published_year", "description", "year_added"], PseudoTableType.Table),
            new PseudoTable("contributors", new ObjectIdentifier("contributors", schema), ["contributor_id", "first_name", "last_name", "description"], PseudoTableType.Table),
            new PseudoTable("book_authors", new ObjectIdentifier("book_authors", schema), ["book_id", "contributor_id", "list_order"], PseudoTableType.Table),
        ]);

        s_pseudoTables3 = new PseudoTableSet(schema, [
            new PseudoTable("table1", new ObjectIdentifier("table1", schema), ["id", "name", "value"], PseudoTableType.Table),
            new PseudoTable("table2", new ObjectIdentifier("table2", schema), ["id", "name", "description"], PseudoTableType.Table),
        ]);
    }

    public static (ConfigBase, Formatter) CreateFormatter(
        PseudoTableSet? pseudoTables,
        bool preferTabs = false
    )
    {
        var config = TestConfig.GetMyTestConfig();

        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.OmitModifiersIfDefault = true;
        config.Formatting.OpeningParensOnNewLine = true;
        config.Formatting.PreferTabs = false;
        config.Formatting.TabSize = 4;

        config.Formatting.PreferTabs = preferTabs;
        var formatter = new Formatter(config, new TextParser(new MyLexer(), new MyParser()), new MyFunctionNameProvider())
        {
            PseudoTables = pseudoTables?.CloneExternalOnly(),
        };
        return (config, formatter);
    }
}
