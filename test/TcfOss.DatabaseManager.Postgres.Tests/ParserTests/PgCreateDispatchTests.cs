using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgCreateDispatchTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("CREATE FUNCTION f(x INT) RETURNS INT RETURN x", typeof(CreateFunction))]
    [InlineData("CREATE OR REPLACE FUNCTION f() RETURNS INT RETURN 1", typeof(CreateFunction))]
    [InlineData("CREATE PROCEDURE p() SELECT 1", typeof(CreateProcedure))]
    [InlineData("CREATE VIEW v AS SELECT 1", typeof(CreateView))]
    [InlineData("CREATE OR REPLACE VIEW v AS SELECT 1", typeof(CreateView))]
    [InlineData("CREATE INDEX ix ON t (id)", typeof(CreateIndex.Standard))]
    [InlineData("CREATE TEMP TABLE t (id INT)", typeof(CreateTable))]
    public void SupportedSharedCreateStatements_Dispatch(string sql, Type expectedType)
    {
        Assert.IsType(expectedType, ParseStatement(sql));
    }

    [Theory]
    [InlineData("CREATE")]
    [InlineData("CREATE *")]
    [InlineData("CREATE unsupported_object")]
    public void InvalidCreateTargets_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE OR REPLACE TABLE t (id INT)")]
    [InlineData("CREATE TEMP INDEX ix ON t (id)")]
    public void UnsupportedCreateOptions_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
