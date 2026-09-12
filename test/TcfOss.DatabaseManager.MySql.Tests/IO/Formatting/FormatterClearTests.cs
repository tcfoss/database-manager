namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatterClearTests
{
    [Fact]
    public void NoClear_AccumulatesOutput()
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            formatter.Format("SELECT 1");
            formatter.Format("SELECT 2");
            string combined = formatter.GetFormatted();

            Assert.Contains("SELECT\n    1;", combined, StringComparison.Ordinal);
            Assert.Contains("SELECT\n    2;", combined, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            formatter.Format("SELECT 1");
            formatter.Clear();
            Assert.Equal("", formatter.GetFormatted());
        }
    }

    [Fact]
    public void Clear_ResetsIndent()
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            formatter.Format("""
                CREATE PROCEDURE `p`()
                BEGIN
                    SELECT 1;
                END
                """);
            formatter.Clear();

            // If indent were not reset, this simple statement would be over-indented.
            string result = formatter.GetFormatted("SELECT 1");
            Assert.Equal("SELECT\n    1;", result, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Clear_ResetsNewLineFlag_NoLeadingBlankLine()
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            formatter.Format("SELECT 1");
            formatter.Clear();

            string result = formatter.GetFormatted("SELECT 2");
            Assert.False(result.StartsWith('\n') || result.StartsWith('\r'),
                "Output after Clear() should not start with a newline.");
        }
    }

    [Fact]
    public void GetFormatted_IsReusable_GivesIndependentResults()
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            string first = formatter.GetFormatted("SELECT 1");
            string second = formatter.GetFormatted("SELECT 2");

            Assert.Equal("SELECT\n    1;", first, ignoreLineEndingDifferences: true);
            Assert.Equal("SELECT\n    2;", second, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Format_AccumulatesUntilClear()
    {
        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            formatter.Format("SELECT 1");
            formatter.Format("SELECT 2");
            string combined = formatter.GetFormatted();

            Assert.Contains("SELECT\n    1;", combined, StringComparison.Ordinal);
            Assert.Contains("SELECT\n    2;", combined, StringComparison.Ordinal);

            // After GetFormatted() the buffer is cleared; next call starts fresh.
            string third = formatter.GetFormatted("SELECT 3");
            Assert.Equal("SELECT\n    3;", third, ignoreLineEndingDifferences: true);
        }
    }
}
