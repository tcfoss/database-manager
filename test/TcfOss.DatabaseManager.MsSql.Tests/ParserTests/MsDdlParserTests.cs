using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsDdlParserTests : ParserTestsBase<MsLexer, MsParser>
{
    // ---------------- CREATE PROCEDURE ----------------

    [Theory]
    [InlineData("CREATE PROCEDURE sp AS SELECT 1", "CREATE PROCEDURE sp () AS SELECT 1", Label = "Bare, no params")]
    [InlineData("CREATE PROCEDURE sp () AS SELECT 1", "CREATE PROCEDURE sp () AS SELECT 1", Label = "Empty parens")]
    [InlineData("CREATE PROCEDURE sp @a INT AS SELECT 1", "CREATE PROCEDURE sp (@a INT) AS SELECT 1", Label = "Bare single param")]
    [InlineData("CREATE PROCEDURE sp (@a INT) AS SELECT 1", "CREATE PROCEDURE sp (@a INT) AS SELECT 1", Label = "Parenthesized single param")]
    [InlineData("CREATE PROCEDURE sp @a INT, @b VARCHAR(50) AS SELECT 1", "CREATE PROCEDURE sp (@a INT, @b VARCHAR(50)) AS SELECT 1", Label = "Multiple bare params")]
    [InlineData("CREATE PROCEDURE sp @a INT = 0, @b VARCHAR(50) = 'x' AS SELECT 1", "CREATE PROCEDURE sp (@a INT = 0, @b VARCHAR(50) = 'x') AS SELECT 1", Label = "Defaults")]
    [InlineData("CREATE PROCEDURE sp @r INT OUTPUT AS SET @r = 1", "CREATE PROCEDURE sp (@r INT OUTPUT) AS SET @r = 1", Label = "OUTPUT param")]
    [InlineData("CREATE PROCEDURE sp @r INT OUT AS SET @r = 1", "CREATE PROCEDURE sp (@r INT OUT) AS SET @r = 1", Label = "OUT keyword round-trips to OUT")]
    [InlineData("CREATE PROCEDURE sp AS BEGIN SELECT 1; SELECT 2; END", "CREATE PROCEDURE sp () AS BEGIN SELECT 1; SELECT 2; END", Label = "BEGIN/END body")]
    [InlineData("CREATE OR ALTER PROCEDURE sp AS SELECT 1", "CREATE OR ALTER PROCEDURE sp () AS SELECT 1", Label = "CREATE OR ALTER")]
    [InlineData("CREATE PROC sp AS SELECT 1", "CREATE PROCEDURE sp () AS SELECT 1", Label = "PROC abbreviation")]
    public void CreateProcedure_TextMatch(string text, string expected)
    {
        var (e, actual) = GetExpectedActual(text, expectedSql: expected, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(e, actual);
    }

    [Theory]
    [InlineData("CREATE PROCEDURE sp WITH RECOMPILE AS SELECT 1", "CREATE PROCEDURE sp () WITH RECOMPILE AS SELECT 1", Label = "WITH RECOMPILE")]
    [InlineData("CREATE PROCEDURE sp WITH ENCRYPTION AS SELECT 1", "CREATE PROCEDURE sp () WITH ENCRYPTION AS SELECT 1", Label = "WITH ENCRYPTION")]
    [InlineData("CREATE PROCEDURE sp WITH ENCRYPTION, RECOMPILE AS SELECT 1", "CREATE PROCEDURE sp () WITH ENCRYPTION, RECOMPILE AS SELECT 1", Label = "WITH ENCRYPTION, RECOMPILE")]
    [InlineData("CREATE PROCEDURE sp WITH EXECUTE AS CALLER AS SELECT 1", "CREATE PROCEDURE sp () WITH EXECUTE AS CALLER AS SELECT 1", Label = "WITH EXECUTE AS CALLER")]
    [InlineData("CREATE PROCEDURE sp WITH EXECUTE AS SELF AS SELECT 1", "CREATE PROCEDURE sp () WITH EXECUTE AS SELF AS SELECT 1", Label = "WITH EXECUTE AS SELF")]
    [InlineData("CREATE PROCEDURE sp WITH EXECUTE AS OWNER AS SELECT 1", "CREATE PROCEDURE sp () WITH EXECUTE AS OWNER AS SELECT 1", Label = "WITH EXECUTE AS OWNER")]
    [InlineData("CREATE PROCEDURE sp WITH EXECUTE AS 'dbo' AS SELECT 1", "CREATE PROCEDURE sp () WITH EXECUTE AS 'dbo' AS SELECT 1", Label = "WITH EXECUTE AS 'username'")]
    [InlineData("CREATE PROCEDURE sp WITH ENCRYPTION, RECOMPILE, EXECUTE AS CALLER AS SELECT 1", "CREATE PROCEDURE sp () WITH ENCRYPTION, RECOMPILE, EXECUTE AS CALLER AS SELECT 1", Label = "All three options")]
    public void CreateProcedure_WithOptions_TextMatch(string text, string expected)
    {
        var (e, actual) = GetExpectedActual(text, expectedSql: expected, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(e, actual);
    }

    [Theory]
    [InlineData("CREATE PROCEDURE sp WITH FOO AS SELECT 1", Label = "Unknown WITH option")]
    [InlineData("CREATE PROCEDURE sp WITH ENCRYPTION, ENCRYPTION AS SELECT 1", Label = "Duplicate ENCRYPTION")]
    [InlineData("CREATE PROCEDURE sp WITH EXECUTE AS BOGUS AS SELECT 1", Label = "Unknown EXECUTE AS target")]
    public void CreateProcedure_InvalidWithOptions_Throws(string text)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement(text));
    }

    [Fact]
    public void CreateProcedure_MissingBodyAfterNoParameterList_Throws()
    {
        Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement("CREATE PROCEDURE sp"));
    }

    // ---------------- CREATE FUNCTION (scalar) ----------------

    [Theory]
    [InlineData("CREATE FUNCTION fn() RETURNS INT AS BEGIN RETURN 1; END", "CREATE FUNCTION fn () RETURNS INT AS BEGIN RETURN 1; END", Label = "Bare scalar")]
    [InlineData("CREATE FUNCTION fn(@x INT) RETURNS INT AS BEGIN RETURN @x; END", "CREATE FUNCTION fn (@x INT) RETURNS INT AS BEGIN RETURN @x; END", Label = "Single param")]
    [InlineData("CREATE FUNCTION fn(@x INT = 0) RETURNS INT AS BEGIN RETURN @x; END", "CREATE FUNCTION fn (@x INT = 0) RETURNS INT AS BEGIN RETURN @x; END", Label = "Default value")]
    [InlineData("CREATE OR ALTER FUNCTION fn() RETURNS INT AS BEGIN RETURN 1; END", "CREATE OR ALTER FUNCTION fn () RETURNS INT AS BEGIN RETURN 1; END", Label = "CREATE OR ALTER")]
    [InlineData("CREATE FUNCTION fn() RETURNS INT WITH ENCRYPTION AS BEGIN RETURN 1; END", "CREATE FUNCTION fn () RETURNS INT WITH ENCRYPTION AS BEGIN RETURN 1; END", Label = "WITH ENCRYPTION")]
    [InlineData("CREATE FUNCTION fn() RETURNS INT WITH EXECUTE AS CALLER AS BEGIN RETURN 1; END", "CREATE FUNCTION fn () RETURNS INT WITH EXECUTE AS CALLER AS BEGIN RETURN 1; END", Label = "WITH EXECUTE AS CALLER")]
    public void CreateFunction_TextMatch(string text, string expected)
    {
        var (e, actual) = GetExpectedActual(text, expectedSql: expected, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(e, actual);
    }

    [Theory]
    [InlineData("CREATE FUNCTION fn() RETURNS INT WITH RECOMPILE AS BEGIN RETURN 1; END", Label = "RECOMPILE not allowed on FUNCTION")]
    [InlineData("CREATE AGGREGATE FUNCTION fn() RETURNS INT AS BEGIN RETURN 1; END", Label = "AGGREGATE not allowed in T-SQL FUNCTION")]
    [InlineData("CREATE FUNCTION fn() RETURNS TABLE (id INT) AS BEGIN RETURN 1; END")]
    [InlineData("CREATE FUNCTION fn() RETURNS SETOF INT AS BEGIN RETURN 1; END")]
    [InlineData("CREATE FUNCTION fn() RETURNS trigger AS BEGIN RETURN 1; END")]
    [InlineData("CREATE FUNCTION fn() RETURNS void AS BEGIN RETURN 1; END")]
    [InlineData("CREATE FUNCTION fn() RETURNS record AS BEGIN RETURN 1; END")]
    public void CreateFunction_Invalid_Throws(string text)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement(text));
    }



    [Theory]
    [InlineData("CREATE PROCEDURE sp (value INT) AS SELECT 1")]
    [InlineData("CREATE FUNCTION fn(value INT) RETURNS INT AS BEGIN RETURN 1; END")]
    public void CreateRoutine_InvalidParameterName_Throws(string text)
    {
        Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(text));
    }

    // ---------------- ROUTINE PROCEDURE OPTIONS ----------------

    [Theory]
    [InlineData("7")]
    [InlineData("NULL")]
    [InlineData("'text'")]
    public void CreateProcedure_ParameterDefault_IsEqualsValue(string value)
    {
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement($"CREATE PROCEDURE sp @value VARCHAR(20) = {value} AS SELECT 1"));
        var parameter = Assert.IsType<StatementRoutineParameter.Undirected>(Assert.Single(procedure.Parameters));
        StatementRoutineParameter common = parameter;

        Assert.NotNull(parameter.ParameterDefault);
        Assert.Same(parameter.ParameterDefault, common.ParameterDefault);
        Assert.False(parameter.ParameterDefault.UseDefaultKeyword);
        Assert.Equal(value, parameter.ParameterDefault.Value.ToSql());
        Assert.Null(common.Direction);
    }

    [Theory]
    [InlineData("CREATE PROCEDURE sp @value INT DEFAULT 1 AS SELECT 1")]
    [InlineData("CREATE PROCEDURE sp @value INT = (1 + 2) AS SELECT 1")]
    [InlineData("CREATE PROCEDURE sp @value INT = 1 + 2 AS SELECT 1")]
    [InlineData("CREATE FUNCTION fn(@value INT DEFAULT 1) RETURNS INT AS BEGIN RETURN 1; END")]
    [InlineData("CREATE FUNCTION fn(@value INT = (1 + 2)) RETURNS INT AS BEGIN RETURN 1; END")]
    public void CreateRoutine_NonConstantOrDefaultKeywordParameterDefault_IsRejected(string text)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement(text));
    }

    [Theory]
    [InlineData("CREATE FUNCTION fn() AS BEGIN RETURN 1; END")]
    [InlineData("CREATE FUNCTION fn(@value INT) AS BEGIN RETURN @value; END")]
    public void CreateFunction_MissingReturns_IsRejected(string text)
    {
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(text));

        Assert.StartsWith("Expected RETURNS.", exception.Message);
    }

    // ---------------- CREATE TRIGGER ----------------

    [Theory]
    [InlineData("CREATE TRIGGER trg ON t AFTER INSERT AS SELECT 1", Label = "AFTER, single event")]
    [InlineData("CREATE TRIGGER trg ON t AFTER INSERT, UPDATE, DELETE AS SELECT 1", Label = "AFTER, multiple events")]
    [InlineData("CREATE TRIGGER trg ON t INSTEAD OF INSERT AS SELECT 1", Label = "INSTEAD OF")]
    [InlineData("CREATE TRIGGER trg ON t INSTEAD OF INSERT, UPDATE AS SELECT 1", Label = "INSTEAD OF, multiple events")]
    [InlineData("CREATE TRIGGER trg ON t FOR INSERT AS SELECT 1", Label = "FOR (alias for AFTER) round-trips to FOR")]
    [InlineData("CREATE OR ALTER TRIGGER trg ON t AFTER INSERT AS SELECT 1", Label = "CREATE OR ALTER")]
    [InlineData("CREATE TRIGGER trg ON dbo.t AFTER INSERT AS SELECT 1", Label = "Schema-qualified table")]
    [InlineData("CREATE TRIGGER trg ON t AFTER INSERT AS BEGIN SELECT 1; SELECT 2; END", Label = "BEGIN/END body")]
    public void CreateTrigger_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE TRIGGER trg ON t BEFORE INSERT AS SELECT 1", Label = "BEFORE rejected in T-SQL")]
    [InlineData("CREATE TRIGGER trg ON t INSERT AS SELECT 1", Label = "Missing trigger time")]
    [InlineData("CREATE TRIGGER trg ON t AFTER INSERT FOR EACH ROW SELECT 1", Label = "MySQL FOR EACH ROW not supported")]
    public void CreateTrigger_Invalid_Throws(string text)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement(text));
    }

    // ---------------- CREATE VIEW ----------------

    [Theory]
    [InlineData("CREATE VIEW v AS SELECT 1 AS a", Label = "Bare")]
    [InlineData("CREATE VIEW v (a) AS SELECT 1", Label = "Column list")]
    [InlineData("CREATE OR ALTER VIEW v AS SELECT 1 AS a", Label = "CREATE OR ALTER")]
    [InlineData("CREATE VIEW v WITH ENCRYPTION AS SELECT 1 AS a", Label = "WITH ENCRYPTION")]
    [InlineData("CREATE VIEW v WITH SCHEMABINDING AS SELECT 1 AS a", Label = "WITH SCHEMABINDING")]
    [InlineData("CREATE VIEW v WITH ENCRYPTION, SCHEMABINDING AS SELECT 1 AS a", Label = "WITH ENCRYPTION, SCHEMABINDING")]
    [InlineData("CREATE VIEW dbo.v WITH SCHEMABINDING AS SELECT 1 AS a", Label = "Schema-qualified")]
    public void CreateView_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE VIEW v WITH FOO AS SELECT 1 AS a", Label = "Unknown WITH option")]
    [InlineData("CREATE VIEW v WITH (security_barrier = TRUE) AS SELECT 1 AS a", Label = "PostgreSQL parenthesized options")]
    [InlineData("CREATE VIEW v WITH (security_invoker) AS SELECT 1 AS a", Label = "PostgreSQL valueless option")]
    [InlineData("CREATE VIEW v WITH ENCRYPTION, ENCRYPTION AS SELECT 1 AS a", Label = "Duplicate ENCRYPTION")]
    [InlineData("CREATE ALGORITHM = MERGE VIEW v AS SELECT 1 AS a", Label = "MySQL ALGORITHM rejected")]
    [InlineData("CREATE DEFINER = `u`@`%` VIEW v AS SELECT 1 AS a", Label = "MySQL DEFINER rejected")]
    public void CreateView_Invalid_Throws(string text)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement(text));
    }
}
