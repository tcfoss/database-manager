namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatCreateEventTests
{
    [Fact]
    public void CreateEvent_Simple_At()
    {
        var text = "CREATE EVENT my_event ON SCHEDULE AT NOW() DO ROLLBACK";
        var formatted = """
        CREATE
        EVENT `my_event`
            ON SCHEDULE AT NOW()
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_Simple_Every()
    {
        var text = "CREATE EVENT my_event ON SCHEDULE EVERY 1 WEEK DO ROLLBACK";
        var formatted = """
        CREATE
        EVENT `my_event`
            ON SCHEDULE EVERY 1 WEEK
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_WithOrReplace_Definer()
    {
        var text = "CREATE OR REPLACE DEFINER = CURRENT_USER EVENT my_event ON SCHEDULE AT NOW() DO ROLLBACK";
        var formatted = """
        CREATE OR REPLACE
            DEFINER = CURRENT_USER
        EVENT `my_event`
            ON SCHEDULE AT NOW()
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_WithIfNotExists()
    {
        var text = "CREATE EVENT IF NOT EXISTS my_event ON SCHEDULE AT NOW() DO ROLLBACK";
        var formatted = """
        CREATE
        EVENT IF NOT EXISTS `my_event`
            ON SCHEDULE AT NOW()
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_WithOnCompletionPreserve()
    {
        var text = "CREATE EVENT my_event ON SCHEDULE AT NOW() ON COMPLETION PRESERVE DO ROLLBACK";
        var formatted = """
        CREATE
        EVENT `my_event`
            ON SCHEDULE AT NOW()
            ON COMPLETION PRESERVE
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_WithOnCompletionNotPreserve()
    {
        var text = "CREATE EVENT my_event ON SCHEDULE AT NOW() ON COMPLETION NOT PRESERVE DO ROLLBACK";
        var formatted = """
        CREATE
        EVENT `my_event`
            ON SCHEDULE AT NOW()
            ON COMPLETION NOT PRESERVE
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_WithDisable()
    {
        var text = "CREATE EVENT my_event ON SCHEDULE AT NOW() DISABLE DO ROLLBACK";
        var formatted = """
        CREATE
        EVENT `my_event`
            ON SCHEDULE AT NOW()
            DISABLE
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_WithComment()
    {
        var text = "CREATE EVENT my_event ON SCHEDULE AT NOW() COMMENT 'test comment' DO ROLLBACK";
        var formatted = """
        CREATE
        EVENT `my_event`
            ON SCHEDULE AT NOW()
            COMMENT 'test comment'
        DO
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_WithBeginEnd()
    {
        var text = "CREATE EVENT my_event ON SCHEDULE AT NOW() DO BEGIN ROLLBACK; END";
        var formatted = """
        CREATE
        EVENT `my_event`
            ON SCHEDULE AT NOW()
        DO
        BEGIN
            ROLLBACK;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateEvent_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nCREATE EVENT my_event ON SCHEDULE AT NOW() DO ROLLBACK;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        CREATE
        EVENT `my_event`
            ON SCHEDULE AT NOW()
        DO
            ROLLBACK;

        /* block comment */
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
