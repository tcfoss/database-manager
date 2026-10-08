namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatDeleteTests
{
    [Fact]
    public void Delete_Simple_NoPseudoTables()
    {
        var text = "DELETE FROM books WHERE published_year < 2000";
        var formatted = """
        DELETE
        FROM `books`
        WHERE `published_year` < 2000;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Delete_Simple_WithPseudoTables()
    {
        var text = "DELETE FROM books WHERE published_year < 2000";
        var formatted = """
        DELETE
        FROM `schema1`.`books`
        WHERE `books`.`published_year` < 2000;
        """;

        using var formatter = Helpers.CreateFormatter(Helpers.s_pseudoTables1, objectNamePrefixWithSchema: true);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Delete_ObjectNamePrefixWithSchema_False()
    {
        var text = "DELETE FROM books WHERE published_year < 2000";
        var formatted = """
        DELETE
        FROM `books`
        WHERE `books`.`published_year` < 2000;
        """;

        using var formatter = Helpers.CreateFormatter(Helpers.s_pseudoTables1, objectNamePrefixWithSchema: false);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Delete_OrderByAndLimit_NoPseudoTables()
    {
        var text = "DELETE FROM books WHERE published_year < 2000 ORDER BY title LIMIT 5";
        var formatted = """
        DELETE
        FROM `books`
        WHERE `published_year` < 2000
        ORDER BY `title`
        LIMIT 5;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Delete_MultiTable_JoinConditionIndent_Null()
    {
        var text = "DELETE books FROM books JOIN authors a ON books.author_id = a.id WHERE a.name = 'Test'";
        var formatted = """
        DELETE `schema1`.`books`
        FROM `schema1`.`books`
        INNER JOIN `schema1`.`authors` AS `a` ON `books`.`author_id` = `a`.`id`
        WHERE `a`.`name` = 'Test';
        """;

        using var formatter = Helpers.CreateFormatter(Helpers.s_pseudoTables1, objectNamePrefixWithSchema: true, joinConditionIndent: null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Delete_MultiTable_JoinConditionIndent_4()
    {
        var text = "DELETE books FROM books JOIN authors a ON books.author_id = a.id WHERE a.name = 'Test'";
        var formatted = """
        DELETE `schema1`.`books`
        FROM `schema1`.`books`
        INNER JOIN `schema1`.`authors` AS `a`
            ON `books`.`author_id` = `a`.`id`
        WHERE `a`.`name` = 'Test';
        """;

        using var formatter = Helpers.CreateFormatter(Helpers.s_pseudoTables1, objectNamePrefixWithSchema: true, joinConditionIndent: 4);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Delete_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nDELETE FROM books WHERE id = 1;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        DELETE
        FROM `books`
        WHERE `id` = 1;

        /* block comment */
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }
}
