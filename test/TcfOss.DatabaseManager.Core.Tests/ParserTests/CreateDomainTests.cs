using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

public class CreateDomainTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("CREATE DOMAIN positive AS INT")]
    [InlineData("CREATE DOMAIN positive INT")]
    [InlineData("CREATE DOMAIN app.positive AS app.number_type")]
    [InlineData("CREATE DOMAIN label AS VARCHAR(20) COLLATE app.collation")]
    [InlineData("CREATE DOMAIN positive AS INT DEFAULT 1")]
    [InlineData("CREATE DOMAIN positive AS INT NOT NULL")]
    [InlineData("CREATE DOMAIN positive AS INT NULL")]
    [InlineData("CREATE DOMAIN positive AS INT DEFAULT 1 NOT NULL CHECK (VALUE > 0)")]
    [InlineData("CREATE DOMAIN positive AS INT CONSTRAINT required NOT NULL CONSTRAINT positive_value CHECK (VALUE > 0)")]
    [InlineData("CREATE DOMAIN bounded AS INT CHECK (VALUE > 0 AND VALUE < 10) CHECK (VALUE <> 5)")]
    [InlineData("CREATE DOMAIN label AS TEXT DEFAULT 'it''s ok' CONSTRAINT optional NULL")]
    public void CreateDomain_RoundTrips(string sql)
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement(sql));

        Assert.Equal(sql, domain.ToSql());
        Assert.Equal(sql, ParseStatement(domain.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("COLLATE app.collation DEFAULT 'x' CHECK (VALUE <> '')")]
    [InlineData("COLLATE app.collation CHECK (VALUE <> '') DEFAULT 'x'")]
    [InlineData("DEFAULT 'x' COLLATE app.collation CHECK (VALUE <> '')")]
    [InlineData("DEFAULT 'x' CHECK (VALUE <> '') COLLATE app.collation")]
    [InlineData("CHECK (VALUE <> '') COLLATE app.collation DEFAULT 'x'")]
    [InlineData("CHECK (VALUE <> '') DEFAULT 'x' COLLATE app.collation")]
    public void ReorderedClauses_ProduceCanonicalSql(string clauses)
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement($"CREATE DOMAIN label AS TEXT {clauses}"));
        const string expected = "CREATE DOMAIN label AS TEXT COLLATE app.collation DEFAULT 'x' CHECK (VALUE <> '')";

        Assert.Equal("app.collation", domain.Collation?.ToSql());
        Assert.Equal("'x'", domain.Default?.ToSql());
        Assert.IsType<StatementDomainConstraint.Check>(Assert.Single(domain.Constraints));
        Assert.Equal(expected, domain.ToSql());
        Assert.Equal(expected, ParseStatement(domain.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("CONSTRAINT required NOT NULL DEFAULT 'x' CONSTRAINT valid CHECK (VALUE <> '') COLLATE app.collation", "CONSTRAINT required NOT NULL CONSTRAINT valid CHECK (VALUE <> '')")]
    [InlineData("CONSTRAINT valid CHECK (VALUE <> '') COLLATE app.collation CONSTRAINT required NOT NULL DEFAULT 'x'", "CONSTRAINT valid CHECK (VALUE <> '') CONSTRAINT required NOT NULL")]
    [InlineData("NULL DEFAULT 'x' CHECK (VALUE <> '') COLLATE app.collation", "NULL CHECK (VALUE <> '')")]
    public void InterleavedClauses_PreserveConstraintOrder(string clauses, string expectedConstraints)
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement($"CREATE DOMAIN label AS TEXT {clauses}"));
        string expected = $"CREATE DOMAIN label AS TEXT COLLATE app.collation DEFAULT 'x' {expectedConstraints}";

        Assert.Equal(2, domain.Constraints.Count);
        Assert.Equal(expected, domain.ToSql());
        Assert.Equal(expected, ParseStatement(domain.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("DEFAULT 'first'", "DEFAULT", "'first'", "default")]
    [InlineData("DEFAULT NULL CHECK (VALUE <> '') COLLATE app.collation", "DEFAULT", "'second'", "default")]
    [InlineData("CHECK (VALUE <> '') DEFAULT 'first' CONSTRAINT required NOT NULL", "DEFAULT", "NULL", "default")]
    [InlineData("COLLATE app.first", "COLLATE", "app.second", "collation")]
    [InlineData("COLLATE app.first DEFAULT 'x' CHECK (VALUE <> '')", "COLLATE", "app.first", "collation")]
    [InlineData("CONSTRAINT required NOT NULL COLLATE app.first CHECK (VALUE <> '')", "COLLATE", "app.second", "collation")]
    public void DuplicateClauses_ReportSecondKeyword(string clauses, string keyword, string value, string attribute)
    {
        string sql = $"CREATE DOMAIN label AS TEXT {clauses}\n  {keyword} {value}";

        var exception = Assert.Throws<ParseException.Duplicate>(() => ParseStatements(sql));

        Assert.Contains(attribute, exception.Message);
        var formatted = new ParseException.WithText(exception, "domain.sql", sql);
        Assert.Contains("In file 'domain.sql' at line 2, column 3", formatted.Message);
        Assert.Contains("\n  ^", formatted.Message);
    }

    [Theory]
    [InlineData("CREATE DOMAIN")]
    [InlineData("CREATE DOMAIN d")]
    [InlineData("CREATE DOMAIN d AS")]
    [InlineData("CREATE DOMAIN d AS INT COLLATE")]
    [InlineData("CREATE DOMAIN d AS INT DEFAULT")]
    [InlineData("CREATE DOMAIN d AS INT NOT")]
    [InlineData("CREATE DOMAIN d AS INT CONSTRAINT")]
    [InlineData("CREATE DOMAIN d AS INT CONSTRAINT named")]
    [InlineData("CREATE DOMAIN d AS INT CONSTRAINT named UNIQUE")]
    [InlineData("CREATE DOMAIN d AS INT CHECK VALUE > 0")]
    [InlineData("CREATE DOMAIN d AS INT CHECK ()")]
    [InlineData("CREATE DOMAIN d AS INT CHECK (VALUE >)")]
    [InlineData("CREATE DOMAIN d AS INT CHECK (VALUE > 0")]
    [InlineData("CREATE OR REPLACE DOMAIN d AS INT")]
    [InlineData("CREATE TEMP DOMAIN d AS INT")]
    public void InvalidDomains_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void DomainValue_IsScopedToCheckExpression()
    {
        var parser = new Parser();
        ParserState state = GetState("(VALUE) VALUE");

        Assert.IsType<DomainValue>(parser.ExpressionParser.ParseDomainCheckExpr(state));
        Assert.IsType<SingleIdentifier>(parser.ExpressionParser.ParseExpr(state));
    }

    [Theory]
    [InlineData("(\"VALUE\")")]
    [InlineData("(`VALUE`)")]
    [InlineData("([VALUE])")]
    public void QuotedValue_RemainsAnIdentifier(string sql)
    {
        var parser = new Parser();

        Assert.IsType<SingleIdentifier>(parser.ExpressionParser.ParseDomainCheckExpr(GetState(sql)));
    }

    [Fact]
    public void InvalidCheck_RestoresExpressionScope()
    {
        var parser = new Parser();
        ParserState state = GetState("(VALUE > ) VALUE");

        Assert.ThrowsAny<ParseException>(() => parser.ExpressionParser.ParseDomainCheckExpr(state));
        state.Next();

        Assert.IsType<SingleIdentifier>(parser.ExpressionParser.ParseExpr(state));
    }

    [Fact]
    public void DefaultAfterCheck_UsesOrdinaryIdentifierSemantics()
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement("CREATE DOMAIN label AS TEXT CHECK (VALUE <> '') DEFAULT value"));

        Assert.IsType<SingleIdentifier>(domain.Default);
        var check = Assert.IsType<StatementDomainConstraint.Check>(Assert.Single(domain.Constraints));
        Assert.IsType<DomainValue>(Assert.IsType<BinaryOperator>(check.Expression).Left);
        Assert.Equal("CREATE DOMAIN label AS TEXT DEFAULT value CHECK (VALUE <> '')", domain.ToSql());
    }

    [Fact]
    public void Domain_PreservesReferencesConstraintsAndSource()
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement("CREATE DOMAIN app.positive AS app.number_type COLLATE app.collation DEFAULT 1 CONSTRAINT required NOT NULL CONSTRAINT positive_value CHECK (VALUE > 0)"));

        Assert.Equal("app.positive", domain.Name.ToSql());
        var dataType = Assert.IsType<DataTypeReference.Named>(domain.DataType);
        Assert.Equal("app.number_type", dataType.Name.ToSql());
        Assert.NotNull(dataType.Name.Source);
        Assert.Equal("app.collation", domain.Collation?.ToSql());
        Assert.Equal("1", domain.Default?.ToSql());
        Assert.Equal(2, domain.Constraints.Count);
        var nullability = Assert.IsType<StatementDomainConstraint.Nullability>(domain.Constraints[0]);
        Assert.True(nullability.NotNull);
        Assert.Equal("required", nullability.Name?.Name);
        var check = Assert.IsType<StatementDomainConstraint.Check>(domain.Constraints[1]);
        Assert.Equal("positive_value", check.Name?.Name);
        var comparison = Assert.IsType<BinaryOperator>(check.Expression);
        Assert.IsType<DomainValue>(comparison.Left);
        Assert.Empty(domain.GetReferencedItems(new ReferencedItemsManager()));
        Assert.NotNull(domain.Name.Source);
        Assert.NotNull(domain.Meta.Start);
        Assert.NotNull(domain.Meta.End);
    }

    [Fact]
    public void CheckValue_IsRecognizedWithinNestedExpressions()
    {
        var domain = Assert.IsType<CreateDomain>(ParseStatement("CREATE DOMAIN positive AS INT CHECK ((value + 1) > abs(value))"));

        Assert.Equal("CREATE DOMAIN positive AS INT CHECK ((VALUE + 1) > abs(VALUE))", domain.ToSql());
    }

    [Fact]
    public void NestedDomainCheckScope_RestoresOuterScope()
    {
        var parser = new Parser();
        ParserState state = GetState("(VALUE) VALUE VALUE");

        using (state.EnterDomainCheck())
        {
            Assert.IsType<DomainValue>(parser.ExpressionParser.ParseDomainCheckExpr(state));
            Assert.IsType<DomainValue>(parser.ExpressionParser.ParseExpr(state));
        }

        Assert.IsType<SingleIdentifier>(parser.ExpressionParser.ParseExpr(state));
    }
}
