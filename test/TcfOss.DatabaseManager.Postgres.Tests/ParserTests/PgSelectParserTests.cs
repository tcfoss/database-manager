using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgSelectParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("SELECT id FROM items ORDER BY id NULLS FIRST", Label = "Nulls first")]
    [InlineData("SELECT id FROM items ORDER BY id DESC NULLS LAST", Label = "Descending nulls last")]
    [InlineData("SELECT SUM(id ORDER BY id NULLS LAST) FROM items", Label = "Aggregate null ordering")]
    [InlineData("SELECT SUM(id) OVER (ORDER BY id NULLS FIRST) FROM items", Label = "Window null ordering")]
    public void NullOrdering_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("SELECT result.value AS value FROM LATERAL app.expand(($1)) WITH ORDINALITY AS result (value, ordinal)", Label = "Normalize lateral function")]
    [InlineData("SELECT result.value AS value FROM LATERAL ROWS FROM (app.first(($1)) AS (value INT), app.second()) WITH ORDINALITY AS result (value, other, ordinal)", Label = "Normalize lateral rows from")]
    [InlineData("SELECT result.value AS value FROM LATERAL (SELECT $1 AS original) AS result (value)", Label = "Normalize lateral derived table")]
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
    public void RowsFrom_PreservesPerFunctionColumnDefinitions()
    {
        var state = GetState("ROWS FROM (app.records() AS (id INT, label TEXT), app.other())");
        var relation = Assert.IsType<TableFactor.RowsFrom>(new PgParser().SelectParser.ParseTableWithJoins(state).Relation);

        Assert.Equal(2, relation.Functions.Count);
        Assert.Equal(new[] { "id", "label" }, relation.Functions[0].Columns!.Select(column => column.Name.Name));
        Assert.Null(relation.Functions[1].Columns);
        Assert.IsType<Eof>(state.Peek());
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

    [Theory]
    [InlineData("SELECT * FROM generate_series(1, 3)", Label = "Unaliased table function")]
    [InlineData("SELECT value FROM app.values_for($1) AS result (value)", Label = "Function with argument and column alias")]
    [InlineData("SELECT * FROM unnest(ARRAY[1, 2]) WITH ORDINALITY AS result (value, ordinal)", Label = "Function with ordinality")]
    [InlineData("SELECT * FROM LATERAL unnest(ARRAY[1, 2]) AS result (value)", Label = "Lateral function")]
    [InlineData("SELECT * FROM items CROSS JOIN LATERAL app.expand(items.value) AS result (value)", Label = "Cross join lateral function")]
    [InlineData("SELECT * FROM items LEFT OUTER JOIN LATERAL (SELECT items.id) AS result (value) ON TRUE", Label = "Left join lateral subquery")]
    [InlineData("SELECT * FROM ROWS FROM (app.first($1), app.second($2))", Label = "Rows from functions with arguments")]
    [InlineData("SELECT * FROM ROWS FROM (app.first(), app.second()) WITH ORDINALITY AS result (first, second, ordinal)", Label = "Rows from with ordinality")]
    [InlineData("SELECT * FROM LATERAL ROWS FROM (app.first(), app.second()) AS result (first, second)", Label = "Lateral rows from")]
    [InlineData("SELECT * FROM ROWS FROM (app.records() AS (id INT, label TEXT COLLATE app.locale)) AS result", Label = "Rows from with column definitions")]
    [InlineData("SELECT * FROM items AS result (value)", Label = "Table with column alias")]
    [InlineData("SELECT * FROM (SELECT 1 AS original) AS result (value)", Label = "Derived table with column alias")]
    [InlineData("SELECT * FROM (items INNER JOIN other ON TRUE) AS result (value)", Label = "Nested join with column alias")]
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
        var state = GetState("LATERAL app.expand($1) WITH ORDINALITY AS result (value, ordinal)", 37);
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

    [Theory]
    [InlineData("SELECT COUNT(*) FILTER (WHERE active) FROM items", Label = "Filtered aggregate")]
    [InlineData("SELECT SUM(value) FILTER (WHERE active) OVER (PARTITION BY category) FROM items", Label = "Filtered window aggregate")]
    [InlineData("SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY value DESC NULLS LAST) FROM items", Label = "Ordered set aggregate")]
    [InlineData("SELECT PERCENTILE_DISC(0.5) WITHIN GROUP (ORDER BY value) FILTER (WHERE active) FROM items", Label = "Filtered ordered set aggregate")]
    [InlineData("SELECT SUM(value) OVER result FROM items WINDOW result AS (PARTITION BY category ORDER BY id NULLS FIRST)", Label = "Named window reference")]
    [InlineData("SELECT SUM(value) OVER (base ORDER BY id) FROM items WINDOW base AS (PARTITION BY category)", Label = "Inherited inline window")]
    [InlineData("SELECT SUM(value) OVER result FROM items WINDOW base AS (PARTITION BY category), result AS (base ORDER BY id)", Label = "Inherited window definition")]
    [InlineData("SELECT COUNT(*) OVER result WINDOW result AS ()", Label = "Window without from clause")]
    public void AggregateModifiersAndNamedWindows_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("SELECT SUM(value) OVER (ROWS UNBOUNDED PRECEDING) FROM items", Label = "Rows unbounded preceding")]
    [InlineData("SELECT SUM(value) OVER (RANGE CURRENT ROW) FROM items", Label = "Range current row")]
    [InlineData("SELECT SUM(value) OVER (ORDER BY id GROUPS 2 PRECEDING) FROM items", Label = "Groups offset preceding")]
    [InlineData("SELECT SUM(value) OVER (ORDER BY id ROWS BETWEEN 2 PRECEDING AND 1 FOLLOWING) FROM items", Label = "Rows between offsets")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) FROM items", Label = "Rows through unbounded following")]
    [InlineData("SELECT SUM(value) OVER (ORDER BY created RANGE BETWEEN INTERVAL '1 day' PRECEDING AND CURRENT ROW) FROM items", Label = "Range interval offset")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW EXCLUDE CURRENT ROW) FROM items", Label = "Exclude current row")]
    [InlineData("SELECT SUM(value) OVER (ORDER BY id GROUPS BETWEEN 1 PRECEDING AND 1 FOLLOWING EXCLUDE GROUP) FROM items", Label = "Exclude group")]
    [InlineData("SELECT SUM(value) OVER (ORDER BY id RANGE UNBOUNDED PRECEDING EXCLUDE TIES) FROM items", Label = "Exclude ties")]
    [InlineData("SELECT SUM(value) OVER (ROWS CURRENT ROW EXCLUDE NO OTHERS) FROM items", Label = "Exclude no others")]
    [InlineData("SELECT SUM(value) OVER (base ROWS BETWEEN ($1 + 1) PRECEDING AND $2 FOLLOWING) FROM items WINDOW base AS (ORDER BY id DESC NULLS LAST)", Label = "Inherited frame with parameters")]
    [InlineData("SELECT SUM(value) OVER result FROM items WINDOW result AS (ORDER BY id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)", Label = "Named following frame")]
    public void WindowFrames_ParseAndRoundTrip(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void AggregateWindows_ReportModifierAndFrameDependenciesWithoutWindowObjects()
    {
        const string sql = "SELECT app.aggregate(value) WITHIN GROUP (ORDER BY app.sort(value) NULLS FIRST) FILTER (WHERE app.keep(value)) AS result, SUM(value) OVER (base ROWS BETWEEN app.start($1) PRECEDING AND app.finish($2) FOLLOWING) AS total FROM app.items GROUP BY app.group_key(value) HAVING COUNT(*) FILTER (WHERE app.visible(value)) > 0 WINDOW base AS (PARTITION BY app.partition_key(value) ORDER BY app.rank(value) NULLS LAST)";
        var statement = Assert.IsType<Select>(ParseStatement(sql));
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        string[] expected = ["app.items", "app.aggregate", "app.sort", "app.keep", "app.start", "app.finish", "app.partition_key", "app.rank", "app.group_key", "app.visible"];
        foreach (string name in expected)
        {
            Assert.Contains(name, names);
        }
        Assert.DoesNotContain("base", names);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Fact]
    public void NamedWindows_PreserveIdentifierSourcesAndFrameStructure()
    {
        const string sql = "SELECT SUM(value) OVER result FROM app.items WINDOW base AS (ORDER BY id NULLS LAST), result AS (base ROWS BETWEEN ($1 + 1) PRECEDING AND CURRENT ROW EXCLUDE NO OTHERS)";
        var state = GetState(sql, 71);
        var statement = Assert.IsType<Select>(ParseStatement(state));
        SimpleSelect query = Assert.IsType<SelectBody.SimpleSelectQuery>(statement.Body).Query;

        WindowDefinition windowDef0 = query.Windows![0];
        Assert.Equal(71, windowDef0.Name.Source!.Value.SourceId);
        var defWindowSpec0 = Assert.IsType<WindowSpec.InlineSpecification>(windowDef0.Specification);

        Assert.Equal(NullsOrder.Last, Assert.Single(defWindowSpec0.OrderBy!).NullsOrder);

        WindowDefinition windowDef1 = query.Windows![1];
        Assert.Equal(71, windowDef1.Name.Source!.Value.SourceId);
        var defWindowSpec1 = Assert.IsType<WindowSpec.InlineSpecification>(windowDef1.Specification);

        Assert.Equal(71, defWindowSpec1.BaseWindow!.Source!.Value.SourceId);

        WindowFrame frame = defWindowSpec1.Frame!;
        Assert.Equal(WindowFrameUnit.Rows, frame.Unit);
        Assert.IsType<Nested>(Assert.IsType<WindowFrameBound.Preceding>(frame.Start).Offset);
        Assert.IsType<WindowFrameBound.CurrentRow>(frame.End);
        Assert.Equal(WindowFrameExclusion.NoOthers, frame.Exclusion);

        var function = Assert.IsType<FunctionCall>(Assert.IsType<SimpleSelectItem.UnnamedExpression>(Assert.Single(query.Selections)).Expression);
        var functionWindow = Assert.IsType<WindowSpec.NamedReference>(function.Over);
        Assert.Equal(71, functionWindow.WindowName.Source!.Value.SourceId);

        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData(false, Label = "Preserve aggregate and window grouping")]
    [InlineData(true, Label = "Flatten aggregate and window grouping")]
    public void AggregateWindows_NormalizeAllComponentsWithRequestedFlattening(bool flatten)
    {
        const string sql = "SELECT app.aggregate((value + 1)) WITHIN GROUP (ORDER BY (value + 1) DESC NULLS LAST) FILTER (WHERE (value + 1) > 0) AS result, SUM(value) OVER (base ROWS BETWEEN ($1 + 1) PRECEDING AND ($2 + 1) FOLLOWING EXCLUDE TIES) AS total FROM app.items WINDOW base AS (PARTITION BY (category + 1) ORDER BY (id + 1) NULLS FIRST)";
        var config = PgTestConfig.GetTestConfig();
        var normalizer = new Normalizer(config, new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Select normalized = normalizer.NormalizeSelect(statement, flatten: flatten);

        SimpleSelect query = Assert.IsType<SelectBody.SimpleSelectQuery>(normalized.Body).Query;
        var aggregate = Assert.IsType<FunctionCall>(Assert.IsType<SimpleSelectItem.ExpressionWithAlias>(query.Selections[0]).Expression);
        var windowFunction = Assert.IsType<FunctionCall>(Assert.IsType<SimpleSelectItem.ExpressionWithAlias>(query.Selections[1]).Expression);
        var windowFunctionOver = Assert.IsType<WindowSpec.InlineSpecification>(windowFunction.Over);
        WindowFrame frame = windowFunctionOver.Frame!;
        WindowSpec.InlineSpecification specification = Assert.IsType<WindowSpec.InlineSpecification>(Assert.Single(query.Windows!).Specification);
        Expression[] expressions =
        [
            Assert.IsType<FunctionArgumentExpression.FunctionExpression>(Assert.IsType<FunctionArgument.Unnamed>(Assert.Single(Assert.IsType<FunctionArguments.List>(aggregate.Arguments).Arguments!)).Argument).Expression,
            Assert.Single(aggregate.WithinGroup!).Expression,
            Assert.IsType<Core.Expressions.BinaryOperator>(aggregate.Filter).Left,
            Assert.IsType<WindowFrameBound.Preceding>(frame.Start).Offset,
            Assert.IsType<WindowFrameBound.Following>(frame.End).Offset,
            Assert.Single(specification.PartitionBy!),
            Assert.Single(specification.OrderBy!).Expression
        ];
        foreach (Expression expression in expressions)
        {
            Assert.Equal(!flatten, expression is Nested);
        }
        Assert.Equal(QuoteStyle.Ansi, windowFunctionOver.BaseWindow!.QuoteStyle);
        Assert.Equal(NullsOrder.Last, Assert.Single(aggregate.WithinGroup!).NullsOrder);
        Assert.Equal(NullsOrder.First, Assert.Single(specification.OrderBy!).NullsOrder);
        Assert.Equal(WindowFrameExclusion.Ties, frame.Exclusion);
        Assert.Equal(normalized, normalizer.NormalizeSelect(normalized, flatten: flatten));
    }

    [Fact]
    public void AggregateWindows_PreserveAtomicBodySpansTerminatorsAndDependencies()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC SELECT COUNT(*) FILTER (WHERE app.visible(id)) FROM app.items; SELECT SUM(value) OVER result FROM app.items WINDOW result AS (ORDER BY app.rank(id) ROWS BETWEEN 1 PRECEDING AND CURRENT ROW EXCLUDE TIES); END";
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Select[] statements = [.. body.Statements.OfType<Select>()];

        Assert.Equal(2, statements.Length);
        foreach (Select statement in statements)
        {
            Assert.NotNull(statement.Meta.Start);
            Assert.NotNull(statement.Meta.End);
            Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]);
        }
        string[] names = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Contains("app.visible", names);
        Assert.Contains("app.rank", names);
        Assert.DoesNotContain("result", names);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("SELECT COUNT(*) FILTER ()", Label = "Filter missing WHERE")]
    [InlineData("SELECT COUNT(*) FILTER (WHERE)", Label = "Filter missing predicate")]
    [InlineData("SELECT COUNT(*) FILTER WHERE active", Label = "Filter missing parentheses")]
    [InlineData("SELECT COUNT(*) FILTER (WHERE active) FILTER (WHERE visible)", Label = "Duplicate aggregate filter")]
    [InlineData("SELECT COUNT(*) OVER () FILTER (WHERE active)", Label = "Filter after over")]
    [InlineData("SELECT f() WITHIN (ORDER BY value)", Label = "Within missing GROUP")]
    [InlineData("SELECT f() WITHIN GROUP (value)", Label = "Within group missing ORDER BY")]
    [InlineData("SELECT f() WITHIN GROUP (ORDER value)", Label = "Within group missing BY")]
    [InlineData("SELECT f() WITHIN GROUP (ORDER BY)", Label = "Empty within group ordering")]
    [InlineData("SELECT SUM(value) OVER () WINDOW result ()", Label = "Window missing AS")]
    [InlineData("SELECT SUM(value) OVER () WINDOW result AS result", Label = "Window missing parentheses")]
    [InlineData("SELECT SUM(value) OVER () WINDOW", Label = "Missing window definition")]
    [InlineData("SELECT SUM(value) OVER () WINDOW result AS (),", Label = "Trailing window definition comma")]
    [InlineData("SELECT SUM(value) OVER (ROWS)", Label = "Missing frame bound")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN 1 PRECEDING)", Label = "Frame missing AND")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN 1 PRECEDING AND)", Label = "Missing frame end")]
    [InlineData("SELECT SUM(value) OVER (ROWS 1)", Label = "Offset missing direction")]
    [InlineData("SELECT SUM(value) OVER (ROWS UNBOUNDED FOLLOWING)", Label = "Unbounded following frame start")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN CURRENT ROW AND UNBOUNDED PRECEDING)", Label = "Unbounded preceding frame end")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN CURRENT ROW AND 1 PRECEDING)", Label = "Preceding end after current start")]
    [InlineData("SELECT SUM(value) OVER (ROWS 1 FOLLOWING)", Label = "Following start with implicit current end")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN 1 FOLLOWING AND CURRENT ROW)", Label = "Current end after following start")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN 1 FOLLOWING AND 1 PRECEDING)", Label = "Preceding end after following start")]
    [InlineData("SELECT SUM(value) OVER (ROWS CURRENT ROW EXCLUDE)", Label = "Missing frame exclusion")]
    [InlineData("SELECT SUM(value) OVER (ROWS CURRENT ROW EXCLUDE NO)", Label = "Missing exclusion OTHERS")]
    [InlineData("SELECT SUM(value) OVER (ROWS CURRENT ROW EXCLUDE CURRENT)", Label = "Missing exclusion ROW")]
    [InlineData("SELECT SUM(value) OVER (ROWS CURRENT ROW EXCLUDE TIES EXCLUDE GROUP)", Label = "Duplicate frame exclusion")]
    [InlineData("SELECT id ORDER BY id NULLS", Label = "Missing null ordering")]
    [InlineData("SELECT id ORDER BY id NULLS MIDDLE", Label = "Invalid null ordering")]
    [InlineData("SELECT id ORDER BY id NULLS FIRST NULLS LAST", Label = "Duplicate null ordering")]
    public void AggregateWindows_RejectMalformedSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }


    [Theory]
    [InlineData("SELECT 1 FOR UPDATE", "SELECT 1 FOR UPDATE", Label = "Update lock without from")]
    [InlineData("SELECT * FROM items FOR NO KEY UPDATE OF items NOWAIT", "SELECT * FROM items FOR NO KEY UPDATE OF items NOWAIT", Label = "No key update with target and nowait")]
    [InlineData("SELECT * FROM items FOR SHARE SKIP LOCKED", "SELECT * FROM items FOR SHARE SKIP LOCKED", Label = "Share skips locked rows")]
    [InlineData("SELECT * FROM items AS a JOIN other AS b ON TRUE FOR KEY SHARE OF a, b", "SELECT * FROM items AS a INNER JOIN other AS b ON TRUE FOR KEY SHARE OF a, b", Label = "Key share with multiple aliases")]
    [InlineData("SELECT * FROM items FOR UPDATE OF items NOWAIT FOR SHARE OF items SKIP LOCKED", "SELECT * FROM items FOR UPDATE OF items NOWAIT FOR SHARE OF items SKIP LOCKED", Label = "Multiple locking clauses")]
    [InlineData("SELECT * FROM items FOR UPDATE LIMIT 2", "SELECT * FROM items LIMIT 2 FOR UPDATE", Label = "Lock before limit")]
    [InlineData("SELECT * FROM items LIMIT 2 FOR UPDATE", "SELECT * FROM items LIMIT 2 FOR UPDATE", Label = "Lock after limit")]
    [InlineData("SELECT * FROM (SELECT * FROM items LIMIT ALL FOR KEY SHARE) AS result FOR UPDATE", "SELECT * FROM (SELECT * FROM items LIMIT ALL FOR KEY SHARE) AS result FOR UPDATE", Label = "Nested lock scope")]
    public void RowLocking_ParseAndRoundTrip(string sql, string expected)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(expected, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("SELECT 1 LIMIT ALL", "SELECT 1 LIMIT ALL", Label = "Unlimited count")]
    [InlineData("SELECT 1 LIMIT NULL OFFSET 2", "SELECT 1 LIMIT NULL OFFSET 2", Label = "Null count and offset")]
    [InlineData("SELECT 1 OFFSET 2 ROWS LIMIT 3", "SELECT 1 LIMIT 3 OFFSET 2", Label = "Offset before limit")]
    [InlineData("SELECT 1 OFFSET $1", "SELECT 1 OFFSET $1", Label = "Offset alone")]
    [InlineData("SELECT 1 FETCH NEXT ROW ONLY", "SELECT 1 FETCH FIRST ROWS ONLY", Label = "Implicit fetch count")]
    [InlineData("SELECT 1 FETCH FIRST (2 + 3) ROWS ONLY OFFSET 1", "SELECT 1 OFFSET 1 FETCH FIRST (2 + 3) ROWS ONLY", Label = "Fetch before offset")]
    [InlineData("SELECT id FROM items ORDER BY id OFFSET 2 ROW FETCH NEXT 3 ROWS WITH TIES", "SELECT id FROM items ORDER BY id OFFSET 2 FETCH FIRST 3 ROWS WITH TIES", Label = "Fetch with ties")]
    [InlineData("SELECT 1 UNION ALL SELECT 2 LIMIT 1", "SELECT 1 UNION ALL SELECT 2 LIMIT 1", Label = "Limit applies to set operation")]
    [InlineData("(SELECT 1 LIMIT 1) OFFSET 2", "(SELECT 1 LIMIT 1) OFFSET 2", Label = "Outer offset preserves inner limit")]
    [InlineData("SELECT 1 OFFSET NULL LIMIT ALL", "SELECT 1 LIMIT ALL OFFSET NULL", Label = "Null offset before unlimited count")]
    [InlineData("WITH result AS (SELECT id FROM items LIMIT ALL FOR KEY SHARE) SELECT id FROM result OFFSET 1", "WITH result AS (SELECT id FROM items LIMIT ALL FOR KEY SHARE) SELECT id FROM result OFFSET 1", Label = "CTE body and consumer tails")]
    public void Pagination_ParseAndRoundTrip(string sql, string expected)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(expected, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }



    [Theory]
    [InlineData("SELECT 1 AS value FROM app.items AS i ORDER BY (app.sort_value()) LIMIT (app.count_rows()) OFFSET (app.skip_rows()) FOR NO KEY UPDATE OF i NOWAIT", false, Label = "Limit without flattening")]
    [InlineData("SELECT 1 AS value FROM app.items AS i ORDER BY (app.sort_value()) LIMIT (app.count_rows()) OFFSET (app.skip_rows()) FOR NO KEY UPDATE OF i NOWAIT", true, Label = "Limit with flattening")]
    [InlineData("SELECT 1 AS value FROM app.items AS i ORDER BY (app.sort_value()) OFFSET (app.skip_rows()) FETCH FIRST (app.count_rows()) ROWS WITH TIES FOR KEY SHARE OF i SKIP LOCKED", false, Label = "Fetch without flattening")]
    [InlineData("SELECT 1 AS value FROM app.items AS i ORDER BY (app.sort_value()) OFFSET (app.skip_rows()) FETCH FIRST (app.count_rows()) ROWS WITH TIES FOR KEY SHARE OF i SKIP LOCKED", true, Label = "Fetch with flattening")]
    [InlineData("SELECT 1 AS value FROM app.items AS i OFFSET (app.skip_rows()) FOR UPDATE OF i", false, Label = "Offset without flattening")]
    [InlineData("SELECT 1 AS value FROM app.items AS i OFFSET (app.skip_rows()) FOR UPDATE OF i", true, Label = "Offset with flattening")]
    [InlineData("SELECT 1 AS value FROM app.items AS i LIMIT ALL FOR SHARE OF i", false, Label = "Unlimited count without flattening")]
    [InlineData("SELECT 1 AS value FROM app.items AS i LIMIT ALL FOR SHARE OF i", true, Label = "Unlimited count with flattening")]
    public void SelectTails_NormalizationPreservesClausesAndFlattening(string sql, bool flatten)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));
        var normalizer = new Normalizer(PgTestConfig.GetTestConfig(), new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));

        Select normalized = normalizer.NormalizeSelect(statement, flatten: flatten);
        Expression? count = normalized.Limit switch
        {
            Limit.PgLimit limit => limit.Count,
            Limit.PgFetch limit => limit.Count,
            _ => null
        };
        Expression? offset = normalized.Limit switch
        {
            Limit.PgLimit limit => limit.Offset,
            Limit.PgFetch limit => limit.Offset,
            Limit.PgOffset limit => limit.Offset,
            _ => null
        };
        if (count != null)
        {
            Assert.Equal(!flatten, count is Nested);
        }
        if (offset != null)
        {
            Assert.Equal(!flatten, offset is Nested);
        }
        if (normalized.OrderBy != null)
        {
            Assert.Equal(!flatten, Assert.Single(normalized.OrderBy).Expression is Nested);
        }
        RowLockingClause originalLock = Assert.Single(statement.RowLocks!);
        RowLockingClause normalizedLock = Assert.Single(normalized.RowLocks!);
        Assert.Equal(originalLock.Strength, normalizedLock.Strength);
        Assert.Equal(originalLock.Wait, normalizedLock.Wait);
        Assert.Equal(QuoteStyle.Ansi, Assert.Single(normalizedLock.Of!).QuoteStyle);
        if (statement.Limit is Limit.PgFetch originalFetch)
        {
            Assert.Equal(originalFetch.WithTies, Assert.IsType<Limit.PgFetch>(normalized.Limit).WithTies);
        }
        Assert.Equal(normalized, normalizer.NormalizeSelect(normalized, flatten: flatten));
    }

    [Theory]
    [InlineData("SELECT 1 FROM app.items AS i ORDER BY app.sort_value() LIMIT app.count_rows() OFFSET app.skip_rows() FOR UPDATE OF i NOWAIT", Label = "Limit dependency traversal")]
    [InlineData("SELECT 1 FROM app.items AS i ORDER BY app.sort_value() OFFSET app.skip_rows() FETCH FIRST app.count_rows() ROWS ONLY FOR SHARE OF i", Label = "Fetch dependency traversal")]
    public void SelectTails_CollectExpressionDependenciesWithoutLockAliasObjects(string sql)
    {
        var statement = Assert.IsType<Select>(ParseStatement(sql));
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Equal(new[] { "app.items", "app.sort_value", "app.count_rows", "app.skip_rows" }.Order(), names.Order());
        Assert.DoesNotContain("i", names);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData(false, Label = "Legacy offset fetch without flattening")]
    [InlineData(true, Label = "Legacy offset fetch with flattening")]
    public void SelectTails_NormalizeLegacyOffsetFetch(bool flatten)
    {
        var statement = Assert.IsType<Select>(ParseStatement("SELECT 1 AS value OFFSET (1 + 2) FETCH FIRST (3 + 4) ROWS ONLY"));
        var fetch = Assert.IsType<Limit.PgFetch>(statement.Limit);
        statement = statement with { Limit = new Limit.OffsetFetch(fetch.Offset!, fetch.Count) };
        var normalizer = new Normalizer(PgTestConfig.GetTestConfig(), new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider()));

        Select normalized = normalizer.NormalizeSelect(statement, flatten: flatten);
        var limit = Assert.IsType<Limit.OffsetFetch>(normalized.Limit);

        Assert.Equal(!flatten, limit.Offset is Nested);
        Assert.Equal(!flatten, limit.Fetch is Nested);
        Assert.Equal(normalized, normalizer.NormalizeSelect(normalized, flatten: flatten));
    }

    [Theory]
    [InlineData("SELECT 1 LIMIT", Label = "Missing limit count")]
    [InlineData("SELECT 1 LIMIT 1, 2", Label = "Reject MySQL comma limit")]
    [InlineData("SELECT 1 LIMIT ALL, 2", Label = "Comma after unlimited count")]
    [InlineData("SELECT 1 OFFSET", Label = "Missing offset")]
    [InlineData("SELECT 1 LIMIT 1 LIMIT 2", Label = "Duplicate limit")]
    [InlineData("SELECT 1 OFFSET 1 OFFSET 2", Label = "Duplicate offset")]
    [InlineData("SELECT 1 FETCH", Label = "Missing fetch mode")]
    [InlineData("SELECT 1 FETCH 2 ROWS ONLY", Label = "Missing first or next")]
    [InlineData("SELECT 1 FETCH FIRST 2 ONLY", Label = "Missing fetch row keyword")]
    [InlineData("SELECT 1 FETCH FIRST 2 ROWS", Label = "Missing fetch ending")]
    [InlineData("SELECT 1 FETCH FIRST 2 ROWS WITH", Label = "Missing ties")]
    [InlineData("SELECT 1 FETCH FIRST 2 ROWS ONLY LIMIT 2", Label = "Fetch and limit conflict")]
    [InlineData("SELECT 1 LIMIT 2 FETCH FIRST 2 ROWS ONLY", Label = "Limit and fetch conflict")]
    [InlineData("SELECT 1 FETCH FIRST ROWS ONLY FETCH FIRST ROWS ONLY", Label = "Duplicate fetch")]
    [InlineData("SELECT 1 FOR", Label = "Missing lock strength")]
    [InlineData("SELECT 1 FOR NO UPDATE", Label = "Missing key in lock strength")]
    [InlineData("SELECT 1 FOR KEY UPDATE", Label = "Invalid key lock strength")]
    [InlineData("SELECT 1 FOR UPDATE OF", Label = "Missing lock target")]
    [InlineData("SELECT 1 FOR UPDATE OF a,", Label = "Trailing lock target comma")]
    [InlineData("SELECT 1 FOR UPDATE OF app.items", Label = "Qualified lock target")]
    [InlineData("SELECT 1 FOR SHARE SKIP", Label = "Missing locked keyword")]
    [InlineData("SELECT 1 FOR SHARE NOWAIT SKIP LOCKED", Label = "Conflicting lock wait modes")]
    [InlineData("SELECT 1 FOR SHARE NOWAIT NOWAIT", Label = "Duplicate lock wait mode")]
    [InlineData("SELECT 1 FOR UPDATE LIMIT 1 FOR SHARE", Label = "Locks on both sides of limit")]
    public void SelectTails_RejectMalformedClauses(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
