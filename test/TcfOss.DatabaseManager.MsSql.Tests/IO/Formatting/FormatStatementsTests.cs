namespace TcfOss.DatabaseManager.MsSql.Tests.IO.Formatting;

public class FormatStatementsTests
{
    [Theory]
    [InlineData("INSERT INTO mytable (col1) OUTPUT inserted.col1 VALUES (@value)", "INSERT INTO [mytable]\n(\n    [col1]\n)\nOUTPUT\n    [inserted].[col1]\nVALUES\n(@value);")]
    [InlineData("DELETE FROM mytable OUTPUT deleted.col1 WHERE col1 = @value", "DELETE\nFROM [mytable]\nOUTPUT\n    [deleted].[col1]\nWHERE [col1] = @value;")]
    [InlineData("SELECT col1 FROM mytable WITH (NOLOCK)", "SELECT\n    [col1]\nFROM [mytable] WITH (NOLOCK);")]
    [InlineData("CREATE VIEW myview (col1) WITH SCHEMABINDING AS SELECT 1", "CREATE\nVIEW [myview] ([col1])\nWITH SCHEMABINDING\nAS\nSELECT\n    1;")]
    [InlineData("CREATE TRIGGER mytrigger ON mytable AFTER INSERT AS PRINT 'changed'", "CREATE\nTRIGGER [mytrigger]\nON [mytable]\nAFTER INSERT\nAS \n    PRINT 'changed';")]
    [InlineData("DROP INDEX ix ON mytable", "DROP INDEX [ix] ON mytable;")]
    [InlineData("RETURN", "RETURN;")]
    public void SqlServerSpecificClauses(string input, string expected)
    {
        using var formatter = Helpers.CreateFormatter(null);
        Assert.Equal(expected, formatter.GetFormatted(input), ignoreLineEndingDifferences: true);
    }

    [Theory]
    [InlineData("IF @condition > 0 PRINT 'yes'", "IF @condition > 0\n    PRINT 'yes';")]
    [InlineData("IF @condition > 0 PRINT 'yes' ELSE PRINT 'no'", "IF @condition > 0\n    PRINT 'yes'\nELSE\n    PRINT 'no';")]
    [InlineData("IF @condition > 0 BEGIN PRINT 'yes'; END ELSE BEGIN PRINT 'no'; END", "IF @condition > 0\nBEGIN\n    PRINT 'yes';\nEND\nELSE\nBEGIN\n    PRINT 'no';\nEND;")]
    public void If_FormatsSingleStatementAndBlockBranches(string input, string expected)
    {
        using var formatter = Helpers.CreateFormatter(null);
        Assert.Equal(expected, formatter.GetFormatted(input), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void UpdateAlias_From_OutputAndWhere()
    {
        var input = "UPDATE t SET t.col1 = @value OUTPUT inserted.col1 FROM mytable t WHERE t.col2 > 0";
        var expected = """
        UPDATE [t]
        SET
            [t].[col1] = @value
        OUTPUT
            [inserted].[col1]
        FROM [mytable] AS [t]
        WHERE [t].[col2] > 0;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        Assert.Equal(expected, formatter.GetFormatted(input), ignoreLineEndingDifferences: true);
    }

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
