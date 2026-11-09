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
}
