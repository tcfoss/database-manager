using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
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
    [InlineData("CREATE DOMAIN positive AS INTEGER CHECK (VALUE > 0)", typeof(CreateDomain))]
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
    [InlineData("CREATE OR REPLACE DOMAIN d AS INTEGER")]
    [InlineData("CREATE TEMP DOMAIN d AS INTEGER")]
    public void UnsupportedCreateOptions_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE DOMAIN positive AS INT DEFAULT 1 NOT NULL CHECK (VALUE > 0)")]
    [InlineData("CREATE DOMAIN label TEXT COLLATE \"C\" DEFAULT 'it''s ok' NULL")]
    [InlineData("CREATE DOMAIN \"App\".\"Label\" AS \"App\".\"BaseType\" CONSTRAINT \"Valid\" CHECK (VALUE <> '')")]
    [InlineData("CREATE DOMAIN bounded AS NUMERIC(10,-2) CHECK (VALUE BETWEEN 0 AND 100)")]
    public void Domains_RoundTripWithPostgresTypesAndIdentifiers(string sql)
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement(sql));

        Assert.Equal(sql, domain.ToSql());
        Assert.Equal(sql, ParseStatement(domain.ToSql()).ToSql());
    }

    [Fact]
    public void DomainBaseType_UsesPostgresTypeParser()
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement("CREATE DOMAIN created AS TIMESTAMPTZ"));

        Assert.IsType<PgDataType.PgTimestamp>(Assert.IsType<DataTypeReference.BuiltIn>(domain.DataType).DataType);
        Assert.Equal("CREATE DOMAIN created AS TIMESTAMP WITH TIME ZONE", domain.ToSql());
    }

    [Theory]
    [InlineData("CREATE DOMAIN 'label' AS TEXT")]
    [InlineData("CREATE DOMAIN [label] AS TEXT")]
    [InlineData("CREATE DOMAIN label AS VARCHAR(0)")]
    [InlineData("CREATE DOMAIN label AS app.")]
    [InlineData("CREATE DOMAIN label AS TEXT COLLATE 'C'")]
    [InlineData("CREATE DOMAIN label AS TEXT CONSTRAINT 'valid' CHECK (VALUE <> '')")]
    [InlineData("CREATE DOMAIN label AS TEXT DEFAULT 'x' DEFAULT 'y'")]
    public void InvalidDomains_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
