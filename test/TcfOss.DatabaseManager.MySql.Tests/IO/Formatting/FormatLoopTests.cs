using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatLoopTests
{
    [Fact]
    public void Loop_Simple()
    {
        var text = "LOOP ROLLBACK; END LOOP";
        var formatted = """
        LOOP
            ROLLBACK;
        END LOOP;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Loop_WithSelect()
    {
        var text = "LOOP SELECT 1; END LOOP";
        var formatted = """
        LOOP
            SELECT
                1;
        END LOOP;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Loop_WithEndLabel()
    {
        var text = "l1: LOOP ROLLBACK; END LOOP l1";
        var formatted = """
        `l1`: LOOP
            ROLLBACK;
        END LOOP `l1`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Loop_WithEndLabel_NoQuote()
    {
        var text = """
        l1: LOOP ROLLBACK; END LOOP l1
        """;

        var formatted = """
        l1: LOOP
            ROLLBACK;
        END LOOP l1;
        """;

        using var formatter = Helpers.CreateFormatter(null, quoting: IdentifierQuotationHandling.IfSpecial);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Loop_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nLOOP ROLLBACK; END LOOP;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        LOOP
            ROLLBACK;
        END LOOP;

        /* block comment */
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Loop_CommentsAndWhitespace()
    {
        var text = """
        -- a comment
        l1: LOOP

        ROLLBACK;

        END LOOP l1;

        /* block comment */
        """;

        var formatted = """
        -- a comment
        `l1`: LOOP

            ROLLBACK;

        END LOOP `l1`;

        /* block comment */
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }
}
