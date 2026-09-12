namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatUpdateTests
{
    [Fact]
    public void Update_Simple_NoPseudoTables()
    {
        var text = "UPDATE books SET title = 'New Title'";
        var formatted = """
        UPDATE `books`
        SET
            `title` = 'New Title';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_MultipleAssignments_WithWhere_NoPseudoTables()
    {
        var text = "UPDATE books SET title = 'New Title', published_year = 2024 WHERE id = 1";
        var formatted = """
        UPDATE `books`
        SET
            `title` = 'New Title',
            `published_year` = 2024
        WHERE `id` = 1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_WithJoinAndWhere_PseudoTables()
    {
        var text = "UPDATE books JOIN authors a ON books.author_id = a.id SET books.title = 'New Title', books.author_id = 5 WHERE books.id = 1";
        var formatted = """
        UPDATE `schema1`.`books`
        INNER JOIN `schema1`.`authors` AS `a` ON `books`.`author_id` = `a`.`id`
        SET
            `books`.`title` = 'New Title',
            `books`.`author_id` = 5
        WHERE `books`.`id` = 1;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.UpdateTargetPrefixWithObject = true;
        config.Formatting.JoinConditionIndent = null;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_PrefixTarget_True_WithPseudoTables()
    {
        var text = "UPDATE books SET title = 'New Title', author_id = 99 WHERE published_year > 2000";
        var formatted = """
        UPDATE `schema1`.`books`
        SET
            `books`.`title` = 'New Title',
            `books`.`author_id` = 99
        WHERE `books`.`published_year` > 2000;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.UpdateTargetPrefixWithObject = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_PrefixTarget_False_WithPseudoTables()
    {
        // UpdateTargetPrefixWithObject = false: SET targets are not prefixed.
        // Column refs in WHERE still use SelectItemPrefixWithObject, so they are prefixed.
        var text = "UPDATE books SET title = 'New Title', author_id = 99 WHERE published_year > 2000";
        var formatted = """
        UPDATE `schema1`.`books`
        SET
            `title` = 'New Title',
            `author_id` = 99
        WHERE `books`.`published_year` > 2000;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.UpdateTargetPrefixWithObject = false;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_JoinConditionIndent_Null()
    {
        var text = "UPDATE books JOIN authors a ON books.author_id = a.id SET title = 'New Title' WHERE published_year > 2000";
        var formatted = """
        UPDATE `schema1`.`books`
        INNER JOIN `schema1`.`authors` AS `a` ON `books`.`author_id` = `a`.`id`
        SET
            `books`.`title` = 'New Title'
        WHERE `books`.`published_year` > 2000;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.UpdateTargetPrefixWithObject = true;
        config.Formatting.JoinConditionIndent = null;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_JoinConditionIndent_4()
    {
        var text = "UPDATE books JOIN authors a ON books.author_id = a.id SET title = 'New Title' WHERE published_year > 2000";
        var formatted = """
        UPDATE `schema1`.`books`
        INNER JOIN `schema1`.`authors` AS `a`
            ON `books`.`author_id` = `a`.`id`
        SET
            `books`.`title` = 'New Title'
        WHERE `books`.`published_year` > 2000;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.UpdateTargetPrefixWithObject = true;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_ObjectNamePrefixWithSchema_False()
    {
        var text = "UPDATE books SET title = 'New Title' WHERE published_year > 2000";
        var formatted = """
        UPDATE `books`
        SET
            `books`.`title` = 'New Title'
        WHERE `books`.`published_year` > 2000;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.UpdateTargetPrefixWithObject = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Update_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nUPDATE books SET title = 'x';\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        UPDATE `books`
        SET
            `title` = 'x';

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

