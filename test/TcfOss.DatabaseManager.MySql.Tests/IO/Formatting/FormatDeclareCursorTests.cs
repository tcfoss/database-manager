namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatDeclareCursorTests
{
    [Fact]
    public void DeclareCursor_Simple()
    {
        var text = "DECLARE my_cursor CURSOR FOR SELECT 1";
        var formatted = """
        DECLARE `my_cursor` CURSOR FOR
            SELECT
                1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareCursor_WithTable()
    {
        var text = "DECLARE my_cursor CURSOR FOR SELECT title FROM books";
        var formatted = """
        DECLARE `my_cursor` CURSOR FOR
            SELECT
                `books`.`title`
            FROM `schema1`.`books`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareCursor_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nDECLARE my_cursor CURSOR FOR SELECT 1;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        DECLARE `my_cursor` CURSOR FOR
            SELECT
                1;

        /* block comment */
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
