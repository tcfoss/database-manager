using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;
using Helpers = TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting.Helpers;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgDropParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("DROP TYPE app.mood")]
    [InlineData("DROP TYPE IF EXISTS app.mood, app.address CASCADE")]
    [InlineData("DROP TYPE \"App\".\"Mood\" RESTRICT")]
    [InlineData("DROP DOMAIN app.label")]
    [InlineData("DROP DOMAIN IF EXISTS app.label, other_label RESTRICT")]
    [InlineData("DROP DOMAIN \"App\".\"Label\" CASCADE")]
    [InlineData("DROP TABLE IF EXISTS app.items, other_items CASCADE")]
    [InlineData("DROP VIEW IF EXISTS app.visible, other_visible RESTRICT")]
    [InlineData("DROP INDEX IF EXISTS app.items_idx, other_idx CASCADE")]
    [InlineData("DROP SCHEMA IF EXISTS app, other_app CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW app.summary RESTRICT")]
    [InlineData("DROP SEQUENCE app.ids CASCADE")]
    [InlineData("DROP TYPE int4, \"char\" CASCADE")]
    [InlineData("DROP TYPE CASCADE")]
    [InlineData("DROP DOMAIN app.label, RESTRICT")]
    public void DropSharedTargets_RoundTripSqlAndJson(string sql)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        var formattedStatement = Assert.IsType<DropObject>(ParseStatement(formatted));
        Assert.Equal(statement.ObjectType, formattedStatement.ObjectType);
        Assert.Equal(statement.IfExists, formattedStatement.IfExists);
        Assert.Equal(statement.PgBehavior, formattedStatement.PgBehavior);
        Assert.Equal(statement.Names.Count, formattedStatement.Names.Count);
        foreach (var (First, Second) in statement.Names.Zip(formattedStatement.Names))
        {
            Assert.Equal(First.Values.Select(identifier => identifier.Name), Second.Values.Select(identifier => identifier.Name));
        }
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("TYPE")]
    [InlineData("DOMAIN")]
    [InlineData("TABLE")]
    [InlineData("VIEW")]
    [InlineData("INDEX")]
    [InlineData("SCHEMA")]
    public void DropSharedTargets_RetainsFlagsAndExplicitBehavior(string target)
    {
        var omitted = Assert.IsType<DropObject>(ParseStatement($"DROP {target} first_name"));
        var restricted = Assert.IsType<DropObject>(ParseStatement($"DROP {target} IF EXISTS first_name, second_name RESTRICT"));
        var cascading = Assert.IsType<DropObject>(ParseStatement($"DROP {target} first_name, second_name CASCADE"));

        Assert.Equal(DroppableObject.Parse(target), omitted.ObjectType);
        Assert.Null(omitted.PgBehavior);
        Assert.False(omitted.IfExists);
        Assert.Single(omitted.Names);
        Assert.True(restricted.IfExists);
        Assert.Equal(2, restricted.Names.Count);
        Assert.Equal(PgDropBehavior.Restrict, restricted.PgBehavior);
        Assert.Equal(PgDropBehavior.Cascade, cascading.PgBehavior);
        Assert.False(cascading.Temporary);
        Assert.Null(cascading.OnObject);
    }

    [Theory]
    [InlineData("TYPE")]
    [InlineData("DOMAIN")]
    public void DropTypeTargets_ReportUnresolvedReferencesAndRestoreScopes(string target)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement($"DROP {target} app.label, label CASCADE"));
        var tableIdentifier = new ObjectIdentifier("label", new SchemaIdentifier("app", new CatalogIdentifier("def")));
        var table = new PseudoTable("label", tableIdentifier, ["label"], PseudoTableType.Table);
        var tables = new PseudoTableSet(null, [table]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All, PseudoTables = tables };
        context.AddLocalVariable(new Identifier("label"));
        using var scope = context.Enter(ReferencedItemsContext.SelectItem);

        var references = statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(["app.label", "label"], references.Select(reference => reference.Name.ToSql()));
        Assert.All(references, reference => Assert.Equal(ItemType.Unknown, reference.Type));
        Assert.All(references, reference => Assert.Null(reference.ObjectHandle));
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Table | ObjectNameFilters.Variable }));
        _ = statement.GetReferencedItems(context).Take(1).ToArray();
        Assert.Equal(ReferencedItemsContext.SelectItem, context.CurrentContext);
        Assert.Same(tables, context.PseudoTables);
        Assert.Same(table, Assert.Single(tables.GetPseudoTables("label")));
    }

    [Fact]
    public void DropTargets_PreserveSourcesCommentsAndStatementBoundaries()
    {
        const string sql = "/* lead */ DROP TYPE IF EXISTS \"App\".\"Mood\", mood CASCADE /* tail */; DROP DOMAIN app.label RESTRICT; SELECT 1";
        var statements = ParseStatements(sql, sourceId: 167);
        var first = Assert.IsType<DropObject>(statements[0]);

        Assert.Equal(3, statements.Count);
        Assert.Equal(0, first.Meta.Start);
        Assert.Equal(sql.IndexOf(';') + 1, first.Meta.EndPlusTerminatorLength);
        Assert.All(first.Names.SelectMany(name => name.Values), identifier => Assert.Equal(167, identifier.Source!.Value.SourceId));
        Assert.Contains(first.Meta.PreNonSql!, trivia => trivia is NonSql.BlockComment { Body: " lead " });
        Assert.Contains(first.Meta.PostNonSql!, trivia => trivia is NonSql.BlockComment { Body: " tail " });
        Assert.IsType<DropObject>(statements[1]);
        Assert.IsType<Select>(statements[2]);
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void DropTargets_PreserveSqlAtomicBody()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC DROP TYPE IF EXISTS app.mood, app.address CASCADE; DROP DOMAIN app.label RESTRICT; DROP TABLE items CASCADE; END";
        var statement = Assert.IsType<CreateProcedure>(ParseStatement(sql));

        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<CreateProcedure>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("DROP TYPE")]
    [InlineData("DROP DOMAIN IF EXISTS")]
    [InlineData("DROP TYPE app.mood,")]
    [InlineData("DROP DOMAIN app.label CASCADE RESTRICT")]
    [InlineData("DROP TABLE items CASCADE, other_items")]
    [InlineData("DROP TYPE app.mood ON items")]
    [InlineData("DROP TYPE app.mood(10)")]
    [InlineData("DROP TYPE app.mood[]")]
    [InlineData("DROP TYPE app.mood CASCADE CASCADE")]
    [InlineData("DROP INDEX items_idx ON items")]
    [InlineData("DROP TEMPORARY TYPE app.mood")]
    [InlineData("DROP TEMPORARY TABLE items")]
    [InlineData("DROP TEMPORARY VIEW visible")]
    [InlineData("DROP DATABASE app CASCADE")]
    public void DropSharedTargets_RejectMalformedAndUnsupportedClauses(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
