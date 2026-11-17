using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

public class ControlFlowParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("  DECLARE myvar INT", "Local variable declaration", 1)]
    [InlineData("  DECLARE mycon CONDITION", "Local variable declaration", 1)]
    [InlineData("\n  DECLARE myvar INT", "Local variable declaration", 2)]
    public void DialectSpecificSyntax_ThrowsLocatedParseException(string sql, string syntax, int line)
    {
        var exception = Assert.Throws<ParseException.DialectRequired>(() => new Parser().Parse(GetTokens(sql)));

        Assert.Contains(syntax + " requires a dialect-specific parser.", exception.Message);
        Assert.Contains("Select a SQL dialect", exception.Message);

        var formatted = new ParseException.WithText(exception, "routine.sql", sql);

        Assert.Contains($"In file 'routine.sql' at line {line}, column 3", formatted.Message);
        Assert.Contains("\n  ^", formatted.Message);
        Assert.Same(exception, formatted.InnerException);
    }

    [Theory]
    [InlineData("IF 1 = 1 THEN SELECT 1; END IF", null)]
    [InlineData("IF 1 = 1 THEN SELECT 1; ELSE SELECT 2; END IF", null)]
    [InlineData("IF 1 = 1 THEN SELECT 1; ELSEIF 1 = 2 THEN SELECT 2; END IF", false)]
    [InlineData("IF 1 = 1 THEN SELECT 1; ELSIF 1 = 2 THEN SELECT 2; END IF", true)]
    public void ParseIf_WithThen_UsesPgMySyntax(string sql, bool? elsIf)
    {
        var statement = ParseStatement(sql);
        var myIf = Assert.IsType<If.My>(statement);
        if (elsIf is not null)
        {
            var elseIf = Assert.Single(myIf.ElseIfs!);
            Assert.Equal(elsIf, elseIf.ElsIf);
        }
    }

    [Fact]
    public void ParseIf_NoThen_FallsBackToMsSyntax()
    {
        var statement = ParseStatement("IF 1 = 1 SELECT 1");
        Assert.IsType<If.Ms>(statement);
    }

    [Theory]
    [InlineData("DECLARE")]
    [InlineData("DECLARE +")]
    [InlineData("DECLARE INVALID HANDLER FOR SQLWARNING SELECT 1")]
    public void InvalidHandlerAction_Throws(string sql)
    {
        var parser = new Parser();

        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() =>
            parser.ControlFlowParser.ParseDeclareConditionHandler(new ParserState(Grammar.All, GetTokens(sql))));

        Assert.Contains("CONTINUE", exception.Message);
        Assert.Contains("EXIT", exception.Message);
        Assert.Contains("UNDO", exception.Message);
    }

    [Fact]
    public void InvalidConditionValue_Throws()
    {
        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() =>
            new Parser().Parse(GetTokens("DECLARE mycon CONDITION FOR UNKNOWN")));

        Assert.Contains("SQLSTATE", exception.Message);
    }

    [Theory]
    [InlineData("DECLARE mycon CONDITION FOR SQLSTATE VALUE 'ABCDE'", "DECLARE mycon CONDITION FOR SQLSTATE VALUE 'ABCDE'")]
    [InlineData("DECLARE CONTINUE HANDLER FOR SQLSTATE VALUE 'ABCDE' SELECT 1", "DECLARE CONTINUE HANDLER FOR SQLSTATE 'ABCDE' SELECT 1")]
    public void Statement_TextMatch(string sql, string expectedSql)
    {
        var (expected, actual) = GetExpectedActual(new Parser().Parse, sql, expectedSql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void EndSubstatements_PreservesSelectedTriviaFromOriginalClosingToken(bool consumeEnd, bool preserveTrivia)
    {
        ParserState state = GetState("SELECT 1; /* boundary */ END; SELECT 2");
        var ender = new EndSubstatements
        {
            GetFinished = s => consumeEnd ? s.ParseKeyword(Keyword.END) : s.PeekKeyword(Keyword.END),
            GetEofException = s => s.ExpectedException("END"),
            PreserveEndTrivia = token => preserveTrivia && token is Word { Keyword: Keyword.END }
        };

        SqlValueList<Statement> statements = new Parser().Parse(state, ender);

        Assert.Single(statements.OfType<Select>());
        if (preserveTrivia)
        {
            var inert = Assert.Single(statements.OfType<InertOnly>());
            var comment = Assert.Single(inert.Meta.PreNonSql!.OfType<Core.Statements.Components.NonSql.BlockComment>());
            Assert.Equal(" boundary ", comment.Body);
        }
        else
        {
            Assert.Empty(statements.OfType<InertOnly>());
        }
        Assert.Equal(!consumeEnd, state.PeekKeyword(Keyword.END));
    }

    [Theory]
    [InlineData("LOOP SELECT 1; END LOOP", typeof(Loop))]
    [InlineData("WHILE TRUE LOOP SELECT 1; END LOOP", typeof(While.Pg))]
    [InlineData("WHILE 1 = 1 DO SELECT 1; END WHILE", typeof(While.My))]
    [InlineData("WHILE 1 = 1 SELECT 1", typeof(While.Ms))]
    [InlineData("FOR row_value IN SELECT 1 LOOP SELECT 1; END LOOP", typeof(PgFor))]
    [InlineData("FOREACH item IN ARRAY app.values() LOOP EXIT; END LOOP", typeof(PgForeach))]
    [InlineData("EXIT label WHEN TRUE", typeof(PgLoopControl.Exit))]
    [InlineData("CONTINUE WHEN TRUE", typeof(PgLoopControl.Continue))]
    [InlineData("PERFORM app.work()", typeof(Select))]
    [InlineData("RETURN", typeof(Return))]
    [InlineData("RETURN NEXT 1", typeof(ReturnNoTerminate.NextExpr))]
    [InlineData("RETURN QUERY SELECT 1", typeof(ReturnNoTerminate.Query))]
    public void ProceduralStatements_DispatchUsingAvailableGrammar(string sql, Type expectedType)
    {
        Statement statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        Assert.IsType(expectedType, statement);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }
}
