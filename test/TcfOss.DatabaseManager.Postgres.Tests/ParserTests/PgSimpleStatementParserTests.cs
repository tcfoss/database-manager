using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgSimpleStatementParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("COMMENT ON TABLE app.items IS 'table'", "app.items")]
    [InlineData("COMMENT ON COLUMN app.items.id IS 'column'", "app.items")]
    [InlineData("COMMENT ON FUNCTION app.f(INT) IS 'function'", "app.f")]
    [InlineData("COMMENT ON TRIGGER audit ON app.items IS 'trigger'", "app.items")]
    public void Comments_ReportTargetsAndRespectReferenceFilters(string sql, string expected)
    {
        var comment = Assert.IsType<CommentOn>(ParseStatement(sql));

        var references = comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(expected, Assert.Single(references).Name.ToSql());
        Assert.Empty(comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("COMMENT TABLE items IS 'text'")]
    [InlineData("COMMENT ON TABLE items 'text'")]
    [InlineData("COMMENT ON TABLE items IS 1")]
    [InlineData("COMMENT ON TABLE items IS TRUE")]
    [InlineData("COMMENT ON TABLE items IS FALSE")]
    [InlineData("COMMENT ON TABLE items IS X'1234'")]
    [InlineData("COMMENT ON TABLE items IS identifier")]
    [InlineData("COMMENT ON TABLE items IS")]
    [InlineData("COMMENT ON FUNCTION f(INT DEFAULT 1) IS 'text'")]
    [InlineData("COMMENT ON TRIGGER app.audit ON items IS 'text'")]
    [InlineData("COMMENT ON TRIGGER audit items IS 'text'")]
    [InlineData("COMMENT ON TABLE items IS 'text' trailing")]
    public void InvalidComments_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void Comments_UnqualifiedColumnTargetsDoNotInventObjectReferences()
    {
        var target = new PgStatementCommentTarget.Column(new ObjectName(new Identifier("id")));
        var comment = new CommentOn(target, new Value.Null());

        Assert.Empty(comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Fact]
    public void Comments_UnsupportedTargetKindsAreRejected()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("COMMENT ON unsupported_kind items IS 'text'"));
    }

    [Theory]
    [InlineData("SELECT * FROM generate_series(1, 3)")]
    [InlineData("SELECT value FROM app.values_for($1) AS result (value)")]
    [InlineData("SELECT * FROM unnest(ARRAY[1, 2]) WITH ORDINALITY AS result (value, ordinal)")]
    [InlineData("SELECT * FROM LATERAL unnest(ARRAY[1, 2]) AS result (value)")]
    [InlineData("SELECT * FROM items CROSS JOIN LATERAL app.expand(items.value) AS result (value)")]
    [InlineData("SELECT * FROM items LEFT OUTER JOIN LATERAL (SELECT items.id) AS result (value) ON TRUE")]
    [InlineData("SELECT * FROM ROWS FROM (app.first($1), app.second($2))")]
    [InlineData("SELECT * FROM ROWS FROM (app.first(), app.second()) WITH ORDINALITY AS result (first, second, ordinal)")]
    [InlineData("SELECT * FROM LATERAL ROWS FROM (app.first(), app.second()) AS result (first, second)")]
    [InlineData("SELECT * FROM ROWS FROM (app.records() AS (id INT, label TEXT COLLATE app.locale)) AS result")]
    [InlineData("SELECT * FROM items AS result (value)")]
    [InlineData("SELECT * FROM (SELECT 1 AS original) AS result (value)")]
    [InlineData("SELECT * FROM (items INNER JOIN other ON TRUE) AS result (value)")]
    public void FromRelations_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void FromFunction_PreservesLateralOrdinalityArgumentsAndAliasColumns()
    {
        var state = new ParserState(GetTokens("LATERAL app.expand($1) WITH ORDINALITY AS result (value, ordinal)"), 37);
        var relation = Assert.IsType<TableFactor.TableFunction>(new PgParser().SelectParser.ParseTableWithJoins(state).Relation);

        Assert.True(relation.IsLateral);
        Assert.True(relation.WithOrdinality);
        Assert.Equal("app.expand", relation.Function.Call.Name.ToSql());
        Assert.IsType<FunctionArguments.List>(relation.Function.Call.Arguments);
        Assert.Equal("result", relation.Alias!.Name);
        Assert.Equal(new[] { "value", "ordinal" }, relation.AliasColumns!.Select(column => column.Name));
        Assert.Equal(37, relation.Alias.Source!.Value.SourceId);
        Assert.IsType<Eof>(state.Peek());
    }

    [Fact]
    public void RowsFrom_PreservesPerFunctionColumnDefinitions()
    {
        var state = GetState("ROWS FROM (app.records() AS (id INT, label TEXT), app.other())");
        var relation = Assert.IsType<TableFactor.RowsFrom>(new PgParser().SelectParser.ParseTableWithJoins(state).Relation);

        Assert.Equal(2, relation.Functions.Count);
        Assert.Equal(new[] { "id", "label" }, relation.Functions[0].Columns!.Select(column => column.Name.Name));
        Assert.Null(relation.Functions[1].Columns);
        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData("SELECT result.value AS value FROM LATERAL app.expand(($1)) WITH ORDINALITY AS result (value, ordinal)")]
    [InlineData("SELECT result.value AS value FROM LATERAL ROWS FROM (app.first(($1)) AS (value INT), app.second()) WITH ORDINALITY AS result (value, other, ordinal)")]
    [InlineData("SELECT result.value AS value FROM LATERAL (SELECT $1 AS original) AS result (value)")]
    public void FromRelations_NormalizeWithoutLosingComponents(string sql)
    {
        var config = PgTestConfig.GetTestConfig();
        var normalizer = new Normalizer(config, new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Select normalized = normalizer.NormalizeSelect(statement);
        string normalizedSql = normalized.ToSql();

        Assert.Contains("LATERAL ", normalizedSql);
        Assert.Contains("AS \"result\" (\"value\"", normalizedSql);
        if (sql.Contains("WITH ORDINALITY", StringComparison.Ordinal))
        {
            Assert.Contains("WITH ORDINALITY", normalizedSql);
        }
        Assert.Equal(normalized, normalizer.NormalizeSelect(normalized));
    }

    [Fact]
    public void FromFunctions_ReportFunctionsAndArgumentReferencesWithoutInventingTables()
    {
        var statement = Assert.IsType<Select>(ParseStatement("SELECT result.value FROM app.items CROSS JOIN LATERAL app.expand(items.value) AS result (value)"));
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        ItemRef[] references = [.. statement.GetReferencedItems(context)];
        string[] objects = [.. references.OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Contains("app.items", objects);
        Assert.Contains("app.expand", objects);
        Assert.DoesNotContain("result", objects);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Fact]
    public void FromFunctions_PreserveAtomicBodyStatementBoundariesAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC SELECT * FROM app.first($1); SELECT * FROM ROWS FROM (app.second(), app.third()); END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Select[] statements = [.. body.Statements.OfType<Select>()];

        Assert.Equal(2, statements.Length);
        foreach (Select statement in statements)
        {
            Assert.NotNull(statement.Meta.EndPlusTerminatorLength);
            Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength.Value - 1]);
        }
        string[] functions = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.first", "app.second", "app.third" }, functions);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("SELECT * FROM ROWS FROM ()")]
    [InlineData("SELECT * FROM ROWS FROM (app.first(),)")]
    [InlineData("SELECT * FROM app.first() AS result ()")]
    [InlineData("SELECT * FROM ROWS FROM (app.records() AS ())")]
    [InlineData("SELECT * FROM LATERAL")]
    public void FromRelations_RejectMalformedComponents(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
