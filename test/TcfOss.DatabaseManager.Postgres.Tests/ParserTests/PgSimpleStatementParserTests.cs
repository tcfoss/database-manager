using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
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
    [InlineData("COMMENT ON TABLE app.items IS 'table'", "app.items", Label = "Table comment")]
    [InlineData("COMMENT ON COLUMN app.items.id IS 'column'", "app.items", Label = "Column comment")]
    [InlineData("COMMENT ON FUNCTION app.f(INT) IS 'function'", "app.f", Label = "Function comment")]
    [InlineData("COMMENT ON TRIGGER audit ON app.items IS 'trigger'", "app.items", Label = "Trigger comment")]
    public void Comments_ReportTargetsAndRespectReferenceFilters(string sql, string expected)
    {
        var comment = Assert.IsType<CommentOn>(ParseStatement(sql));

        var references = comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }).OfType<ItemRef.ObjectNameRef>().ToArray();

        Assert.Equal(expected, Assert.Single(references).Name.ToSql());
        Assert.Empty(comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("COMMENT TABLE items IS 'text'", Label = "Missing ON")]
    [InlineData("COMMENT ON TABLE items 'text'", Label = "Missing IS")]
    [InlineData("COMMENT ON TABLE items IS 1", Label = "Numeric comment")]
    [InlineData("COMMENT ON TABLE items IS TRUE", Label = "TRUE comment")]
    [InlineData("COMMENT ON TABLE items IS FALSE", Label = "FALSE comment")]
    [InlineData("COMMENT ON TABLE items IS X'1234'", Label = "Bit string comment")]
    [InlineData("COMMENT ON TABLE items IS identifier", Label = "Identifier comment")]
    [InlineData("COMMENT ON TABLE items IS", Label = "Missing comment value")]
    [InlineData("COMMENT ON FUNCTION f(INT DEFAULT 1) IS 'text'", Label = "Function signature with default")]
    [InlineData("COMMENT ON TRIGGER app.audit ON items IS 'text'", Label = "Qualified trigger name")]
    [InlineData("COMMENT ON TRIGGER audit items IS 'text'", Label = "Missing trigger ON")]
    [InlineData("COMMENT ON TABLE items IS 'text' trailing", Label = "Trailing comment tokens")]
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

    [Fact]
    public void SelectTails_PreserveLockTargetSourcesAndAtomicBodyBoundaries()
    {
        const string sql = "CREATE PROCEDURE app.run() LANGUAGE SQL BEGIN ATOMIC SELECT 1 FROM app.items AS i ORDER BY app.sort_value() LIMIT app.count_rows() OFFSET app.skip_rows() FOR NO KEY UPDATE OF i NOWAIT; SELECT 2 AS value FETCH NEXT ROW ONLY; END";
        var state = new ParserState(GetTokens(sql), 73);
        var procedure = Assert.IsType<CreateProcedure>(new PgParser().ParseStatement(state));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Select[] selects = [.. body.Statements.OfType<Select>()];

        Assert.Equal(2, selects.Length);
        Identifier target = Assert.Single(Assert.Single(selects[0].RowLocks!).Of!);
        Assert.Equal(73, target.Source!.Value.SourceId);
        Assert.Equal("i", target.Name);
        foreach (Select select in selects)
        {
            Assert.NotNull(select.Meta.Start);
            Assert.NotNull(select.Meta.End);
            Assert.Equal(';', sql[select.Meta.EndPlusTerminatorLength!.Value - 1]);
            Assert.Equal(select.Meta.End + 1, select.Meta.EndPlusTerminatorLength);
        }
        Assert.Contains("FOR NO KEY UPDATE OF i NOWAIT", sql[selects[0].Meta.Start!.Value..selects[0].Meta.End!.Value]);
        Assert.Contains("FETCH NEXT ROW ONLY", sql[selects[1].Meta.Start!.Value..selects[1].Meta.End!.Value]);
        string[] names = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];
        Assert.Equal(new[] { "app.items", "app.sort_value", "app.count_rows", "app.skip_rows" }, names);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
        Assert.IsType<Eof>(state.Peek());
    }
}
