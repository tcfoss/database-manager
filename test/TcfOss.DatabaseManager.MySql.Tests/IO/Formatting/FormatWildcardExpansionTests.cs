namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatWildcardExpansionTests
{
    [Fact]
    public void Wildcard_SingleTable()
    {
        var text = "SELECT * FROM books";
        var formatted = """
        SELECT
            `books`.`title`,
            `books`.`author_id`,
            `books`.`published_year`
        FROM `schema1`.`books`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Wildcard_MultipleTables_Join()
    {
        var text = "SELECT * FROM books JOIN authors ON books.author_id = authors.id";
        var formatted = """
        SELECT
            `books`.`title`,
            `books`.`author_id`,
            `books`.`published_year`,
            `authors`.`id`,
            `authors`.`name`
        FROM `schema1`.`books`
        INNER JOIN `schema1`.`authors` ON `books`.`author_id` = `authors`.`id`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void QualifiedWildcard_ByTableName()
    {
        var text = "SELECT books.* FROM books";
        var formatted = """
        SELECT
            `books`.`title`,
            `books`.`author_id`,
            `books`.`published_year`
        FROM `schema1`.`books`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void QualifiedWildcard_ByAlias()
    {
        var text = "SELECT b.* FROM books AS b";
        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`author_id`,
            `b`.`published_year`
        FROM `schema1`.`books` AS `b`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void QualifiedWildcard_SelectSpecificTable_FromJoin()
    {
        var text = "SELECT authors.* FROM books JOIN authors ON books.author_id = authors.id";
        var formatted = """
        SELECT
            `authors`.`id`,
            `authors`.`name`
        FROM `schema1`.`books`
        INNER JOIN `schema1`.`authors` ON `books`.`author_id` = `authors`.`id`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Wildcard_MixedWithNamedColumns()
    {
        var text = "SELECT authors.*, books.title FROM books JOIN authors ON books.author_id = authors.id";
        var formatted = """
        SELECT
            `authors`.`id`,
            `authors`.`name`,
            `books`.`title`
        FROM `schema1`.`books`
        INNER JOIN `schema1`.`authors` ON `books`.`author_id` = `authors`.`id`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Wildcard_Disabled_StaysAsIs()
    {
        var text = "SELECT * FROM books";
        var formatted = """
        SELECT
            *
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
    public void Wildcard_NoPseudoTables_StaysAsIs()
    {
        var text = "SELECT * FROM books";
        var formatted = """
        SELECT
            *
        FROM `books`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void QualifiedWildcard_UnknownTable_StaysAsIs()
    {
        var text = "SELECT unknown_table.* FROM books";
        var formatted = """
        SELECT
            `unknown_table`.*
        FROM `schema1`.`books`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ExpandWildcards = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
