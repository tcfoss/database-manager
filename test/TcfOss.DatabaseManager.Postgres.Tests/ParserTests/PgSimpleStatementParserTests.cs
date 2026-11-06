using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
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
}
