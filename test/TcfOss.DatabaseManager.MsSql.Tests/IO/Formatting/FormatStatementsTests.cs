namespace TcfOss.DatabaseManager.MsSql.Tests.IO.Formatting;

public class FormatStatementsTests
{
    [Theory]
    [InlineData("break", "BREAK;")]
    [InlineData("continue", "CONTINUE;")]
    [InlineData("while @count < 3 break", "WHILE @count < 3\n    BREAK;")]
    [InlineData("while @count < 3 continue", "WHILE @count < 3\n    CONTINUE;")]
    [InlineData("-- before\nbreak; -- after", "-- before\nBREAK;  -- after")]
    [InlineData("-- before\ncontinue; -- after", "-- before\nCONTINUE;  -- after")]
    public void LoopControl(string input, string expected)
    {
        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void While_BeginEndBody_FormatsLoopControls()
    {
        var input = "WHILE @count < 3 BEGIN CONTINUE; BREAK; END";
        var expected = """
        WHILE @count < 3
        BEGIN
            CONTINUE;
            BREAK;
        END;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void While_NestedLoops_RestoresIndentationForFollowingStatements()
    {
        var input = "BEGIN WHILE @outer > 0 BEGIN WHILE @inner > 0 CONTINUE; BREAK; END; PRINT 'done'; END";
        var expected = """
        BEGIN
            WHILE @outer > 0
            BEGIN
                WHILE @inner > 0
                    CONTINUE;
                BREAK;
            END;
            PRINT 'done';
        END;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Theory]
    [InlineData("begin transaction", "BEGIN TRANSACTION;")]
    [InlineData("begin tran", "BEGIN TRAN;")]
    [InlineData("begin transaction my_tx", "BEGIN TRANSACTION [my_tx];")]
    [InlineData("begin tran @tx", "BEGIN TRAN @tx;")]
    [InlineData("commit", "COMMIT;")]
    [InlineData("commit transaction", "COMMIT TRANSACTION;")]
    [InlineData("commit tran", "COMMIT TRAN;")]
    [InlineData("commit transaction my_tx", "COMMIT TRANSACTION [my_tx];")]
    [InlineData("commit tran @tx", "COMMIT TRAN @tx;")]
    [InlineData("rollback", "ROLLBACK;")]
    [InlineData("rollback transaction", "ROLLBACK TRANSACTION;")]
    [InlineData("rollback tran", "ROLLBACK TRAN;")]
    [InlineData("rollback transaction my_tx", "ROLLBACK TRANSACTION [my_tx];")]
    [InlineData("rollback tran @tx", "ROLLBACK TRAN @tx;")]
    [InlineData("save transaction my_savepoint", "SAVE TRANSACTION [my_savepoint];")]
    [InlineData("save tran @savepoint", "SAVE TRAN @savepoint;")]
    public void Transaction(string input, string expected)
    {
        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Theory]
    [InlineData("print 'hello'", "PRINT 'hello';")]
    [InlineData("print @message", "PRINT @message;")]
    [InlineData("throw", "THROW;")]
    [InlineData("throw 50000, 'oops', 1", "THROW 50000, 'oops', 1;")]
    [InlineData("throw @number, @message, @state", "THROW @number, @message, @state;")]
    [InlineData("raiserror('oops', 16, 1)", "RAISERROR('oops', 16, 1);")]
    [InlineData("raiserror('value: %d', 16, 1, 42)", "RAISERROR('value: %d', 16, 1, 42);")]
    [InlineData("raiserror('%s: %d', 16, 1, @message, @number)", "RAISERROR('%s: %d', 16, 1, @message, @number);")]
    public void Message(string input, string expected)
    {
        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void TryCatch()
    {
        var input = "BEGIN TRY BEGIN TRANSACTION; PRINT 'started'; COMMIT; END TRY BEGIN CATCH ROLLBACK; THROW; END CATCH";
        var expected = """
        BEGIN TRY
            BEGIN TRANSACTION;
            PRINT 'started';
            COMMIT;
        END TRY
        BEGIN CATCH
            ROLLBACK;
            THROW;
        END CATCH;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }
}
