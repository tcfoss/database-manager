using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgExpressionOperatorTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("value::INT")]
    [InlineData("(left + right)::NUMERIC(10,2)")]
    [InlineData("value::TEXT[]")]
    public void PostfixCast_ParsesAndRoundTrips(string sql)
    {
        Cast cast = Assert.IsType<Cast>(ParseExpression(sql));

        Assert.True(cast.UsePostgresCastSyntax);
        Assert.Contains("::", cast.ToSql());
        Assert.Equal(cast, ParseExpression(cast.ToSql()));
    }

    [Fact]
    public void CoreParser_AcceptsPostgresPostfixCast()
    {
        var parser = new Parser();
        Token[] tokens = [.. new PgLexer().Tokenize("value::INT")];

        Cast cast = Assert.IsType<Cast>(parser.ExpressionParser.ParseExpr(new ParserState(tokens)));
        Assert.True(cast.UsePostgresCastSyntax);
    }
}
