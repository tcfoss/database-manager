using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsTransactionParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    [InlineData("BEGIN TRANSACTION", Label = "BEGIN TRANSACTION")]
    [InlineData("BEGIN TRAN", Label = "BEGIN TRAN")]
    [InlineData("BEGIN TRANSACTION my_tx", Label = "BEGIN TRANSACTION named")]
    [InlineData("BEGIN TRAN my_tx", Label = "BEGIN TRAN named")]
    public void BeginTransaction_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("COMMIT", Label = "Bare COMMIT")]
    [InlineData("COMMIT TRANSACTION", Label = "COMMIT TRANSACTION")]
    [InlineData("COMMIT TRAN", Label = "COMMIT TRAN")]
    [InlineData("COMMIT TRANSACTION my_tx", Label = "COMMIT TRANSACTION named")]
    [InlineData("COMMIT TRAN my_tx", Label = "COMMIT TRAN named")]
    public void Commit_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ROLLBACK", Label = "Bare ROLLBACK")]
    [InlineData("ROLLBACK TRANSACTION", Label = "ROLLBACK TRANSACTION")]
    [InlineData("ROLLBACK TRAN", Label = "ROLLBACK TRAN")]
    [InlineData("ROLLBACK TRANSACTION my_tx", Label = "ROLLBACK TRANSACTION named")]
    [InlineData("ROLLBACK TRAN my_savepoint", Label = "ROLLBACK TRAN to savepoint")]
    public void Rollback_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SAVE TRANSACTION my_savepoint", Label = "SAVE TRANSACTION")]
    [InlineData("SAVE TRAN my_savepoint", Label = "SAVE TRAN")]
    public void SaveTransaction_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH SELECT 2; END CATCH", Label = "Simple TRY/CATCH")]
    [InlineData("BEGIN TRY SELECT 1; SELECT 2; END TRY BEGIN CATCH SELECT 3; END CATCH", Label = "Multiple statements in TRY")]
    [InlineData("BEGIN TRY BEGIN TRANSACTION; INSERT INTO t VALUES (1); COMMIT; END TRY BEGIN CATCH ROLLBACK; END CATCH", Label = "TRY/CATCH around transaction")]
    public void TryCatch_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NestedTryCatch_RoundTrips()
    {
        var text = "BEGIN TRY BEGIN TRY SELECT 1; END TRY BEGIN CATCH SELECT 2; END CATCH; END TRY BEGIN CATCH SELECT 3; END CATCH";
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }
}
