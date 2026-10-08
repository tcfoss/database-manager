namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatAlterTableTests
{
    [Fact]
    public void AlterTable_AddColumn()
    {
        var text = "ALTER TABLE books ADD COLUMN isbn VARCHAR(13) NOT NULL";
        var formatted = """
        ALTER TABLE `books`
            ADD COLUMN `isbn` VARCHAR(13) NOT NULL;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_AddColumn_After()
    {
        var text = "ALTER TABLE books ADD COLUMN isbn VARCHAR(13) NOT NULL AFTER title";
        var formatted = """
        ALTER TABLE `books`
            ADD COLUMN `isbn` VARCHAR(13) NOT NULL AFTER `title`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_DropColumn()
    {
        var text = "ALTER TABLE books DROP COLUMN isbn";
        var formatted = """
        ALTER TABLE `books`
            DROP COLUMN `isbn`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_RenameColumn()
    {
        var text = "ALTER TABLE books RENAME COLUMN title TO book_title";
        var formatted = """
        ALTER TABLE `books`
            RENAME COLUMN `title` TO `book_title`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_RenameTable()
    {
        var text = "ALTER TABLE books RENAME TO library_books";
        var formatted = """
        ALTER TABLE `books`
            RENAME TO `library_books`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_MultipleOperations()
    {
        var text = "ALTER TABLE books ADD COLUMN isbn VARCHAR(13) NOT NULL, DROP COLUMN old_col";
        var formatted = """
        ALTER TABLE `books`
            ADD COLUMN `isbn` VARCHAR(13) NOT NULL,
            DROP COLUMN `old_col`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_AddKey()
    {
        var text = "ALTER TABLE books ADD KEY idx_title (title)";
        var formatted = """
        ALTER TABLE `books`
            ADD KEY `idx_title` (`title`);
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_DropKey()
    {
        var text = "ALTER TABLE books DROP KEY idx_title";
        var formatted = """
        ALTER TABLE `books`
            DROP KEY `idx_title`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_AddForeignKey()
    {
        var text = "ALTER TABLE books ADD CONSTRAINT fk_author FOREIGN KEY (author_id) REFERENCES authors (id)";
        var formatted = """
        ALTER TABLE `books`
            ADD CONSTRAINT `fk_author` FOREIGN KEY (`author_id`) REFERENCES `authors` (`id`);
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_DropForeignKey()
    {
        var text = "ALTER TABLE books DROP FOREIGN KEY fk_author";
        var formatted = """
        ALTER TABLE `books`
            DROP FOREIGN KEY `fk_author`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AlterTable_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nALTER TABLE books DROP KEY idx_title;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        ALTER TABLE `books`
            DROP KEY `idx_title`;

        /* block comment */
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }
}
