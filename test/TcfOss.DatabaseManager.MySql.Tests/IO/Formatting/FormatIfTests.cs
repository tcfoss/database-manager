namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatIfTests
{
    [Fact]
    public void If_Simple()
    {
        var text = "IF 1 = 1 THEN ROLLBACK; END IF";
        var formatted = """
        IF 1 = 1 THEN
            ROLLBACK;
        END IF;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void If_Else()
    {
        var text = "IF 1 = 1 THEN ROLLBACK; ELSE RESIGNAL; END IF";
        var formatted = """
        IF 1 = 1 THEN
            ROLLBACK;
        ELSE
            RESIGNAL;
        END IF;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void If_ElseIf()
    {
        var text = "IF 1 = 1 THEN ROLLBACK; ELSEIF 2 = 2 THEN RESIGNAL; END IF";
        var formatted = """
        IF 1 = 1 THEN
            ROLLBACK;
        ELSEIF 2 = 2 THEN
            RESIGNAL;
        END IF;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void If_ElseIf_Else()
    {
        var text = "IF 1 = 1 THEN ROLLBACK; ELSEIF 2 = 2 THEN RESIGNAL; ELSE ROLLBACK; END IF";
        var formatted = """
        IF 1 = 1 THEN
            ROLLBACK;
        ELSEIF 2 = 2 THEN
            RESIGNAL;
        ELSE
            ROLLBACK;
        END IF;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void If_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nIF 1 = 1 THEN ROLLBACK; END IF;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        IF 1 = 1 THEN
            ROLLBACK;
        END IF;

        /* block comment */
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void If_ElseIf_Else_CommentsAndWhitespace()
    {
        var text = """
        -- a comment
        IF 1 = 1 THEN ROLLBACK; ELSEIF
        2 = 2 THEN
                        RESIGNAL; ELSE

        SELECT 1; COMMIT;

        END IF;

        /* block comment */
        """;
        var formatted = """
        -- a comment
        IF 1 = 1 THEN
            ROLLBACK;
        ELSEIF 2 = 2 THEN
            RESIGNAL;
        ELSE

            SELECT
                1;
            COMMIT;

        END IF;

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
