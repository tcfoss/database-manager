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

    [Theory]
    [InlineData("WHILE value > 0 DO SET value = value - 1; END WHILE", "WHILE `value` > 0 DO", "END WHILE;")]
    [InlineData("label: WHILE value > 0 DO SET value = value - 1; /* closing */ END WHILE label", "`label`: WHILE `value` > 0 DO", "END WHILE `label`;")]
    public void While_FormatsBlockedBodyAndLabels(string sql, string expectedHeader, string expectedEnd)
    {
        using var formatter = Helpers.CreateFormatter(null);
        string formatted = formatter.GetFormatted(sql);

        Assert.StartsWith(expectedHeader, formatted);
        Assert.EndsWith(expectedEnd, formatted);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
        if (sql.Contains("/* closing */", StringComparison.Ordinal))
        {
            Assert.Equal(1, formatted.Split("/* closing */", StringSplitOptions.None).Length - 1);
        }
    }
}
