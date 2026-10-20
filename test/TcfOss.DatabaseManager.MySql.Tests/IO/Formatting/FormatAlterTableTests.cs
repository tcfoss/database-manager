using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatAlterTableTests
{
    [Theory]
    [InlineData("ADD COLUMN `mycolumn` VARCHAR(20) NOT NULL", "ADD COLUMN `mycolumn` VARCHAR(20) NOT NULL")]
    [InlineData("ADD `mycolumn` VARCHAR(20) NOT NULL", "ADD `mycolumn` VARCHAR(20) NOT NULL")]
    [InlineData("CHANGE COLUMN title book_title VARCHAR(100)", "CHANGE COLUMN `title` `book_title` VARCHAR(100)")]
    [InlineData("CHANGE COLUMN title book_title VARCHAR(100) FIRST", "CHANGE COLUMN `title` `book_title` VARCHAR(100) FIRST")]
    [InlineData("CHANGE COLUMN title book_title VARCHAR(100) AFTER id", "CHANGE COLUMN `title` `book_title` VARCHAR(100) AFTER `id`")]
    [InlineData("MODIFY COLUMN title VARCHAR(100)", "MODIFY COLUMN `title` VARCHAR(100)")]
    [InlineData("MODIFY COLUMN title VARCHAR(100) FIRST", "MODIFY COLUMN `title` VARCHAR(100) FIRST")]
    [InlineData("MODIFY COLUMN title VARCHAR(100) AFTER id", "MODIFY COLUMN `title` VARCHAR(100) AFTER `id`")]
    [InlineData("ALTER COLUMN title SET DEFAULT 'untitled'", "ALTER COLUMN `title` SET DEFAULT 'untitled'")]
    [InlineData("ALTER COLUMN title DROP DEFAULT", "ALTER COLUMN `title` DROP DEFAULT")]
    [InlineData("ADD PRIMARY KEY (id)", "ADD PRIMARY KEY (`id`)")]
    [InlineData("DROP PRIMARY KEY", "DROP PRIMARY KEY")]
    [InlineData("ADD CONSTRAINT uq UNIQUE (title)", "ADD CONSTRAINT `uq` UNIQUE (`title`)")]
    [InlineData("DROP CONSTRAINT uq", "DROP CONSTRAINT `uq`")]
    [InlineData("ADD CONSTRAINT ck CHECK (id > 0)", "ADD CONSTRAINT `ck` CHECK (`id` > 0)")]
    [InlineData("DROP CHECK ck", "DROP CONSTRAINT `ck`")]
    [InlineData("ADD FULLTEXT KEY ix (title)", "ADD FULLTEXT KEY `ix` (`title`)")]
    [InlineData("ADD SPATIAL KEY ix (location)", "ADD SPATIAL KEY `ix` (`location`)")]
    [InlineData("AUTO_INCREMENT = 100", "AUTO_INCREMENT = 100")]
    public void AlterTable_FormatsOperationVariants(string operation, string expectedOperation)
    {
        using var formatter = Helpers.CreateFormatter(null);

        var actual = formatter.GetFormatted($"ALTER TABLE books {operation}");

        Assert.Equal($"ALTER TABLE `books`\n    {expectedOperation};", actual, ignoreLineEndingDifferences: true);
    }

    [Theory]
    [InlineData("utf8mb4", null, false, false, "CHARACTER SET utf8mb4", null)]
    [InlineData(null, "utf8mb4_general_ci", false, false, "COLLATE utf8mb4_general_ci", null)]
    [InlineData("utf8mb4", "utf8mb4_general_ci", false, false, "CHARACTER SET utf8mb4", "COLLATE utf8mb4_general_ci")]
    [InlineData("utf8mb4", null, true, false, "DEFAULT CHARACTER SET utf8mb4", null)]
    [InlineData(null, "utf8mb4_general_ci", true, false, "DEFAULT COLLATE utf8mb4_general_ci", null)]
    [InlineData("utf8mb4", "utf8mb4_general_ci", true, false, "DEFAULT CHARACTER SET utf8mb4", "DEFAULT COLLATE utf8mb4_general_ci")]
    [InlineData("utf8mb4", null, true, true, "DEFAULT CHARACTER SET = utf8mb4", null)]
    [InlineData(null, "utf8mb4_general_ci", true, true, "DEFAULT COLLATE = utf8mb4_general_ci", null)]
    [InlineData("utf8mb4", "utf8mb4_general_ci", true, true, "DEFAULT CHARACTER SET = utf8mb4", "DEFAULT COLLATE = utf8mb4_general_ci")]
    public void AlterTable_FormatsCharacterSetAst(string? charset, string? collation, bool includeDefault, bool includeEquals, string expectedOperation1, string? expectedOperation2)
    {
        var operations = new SqlValueList<AlterTableOperation>();
        if (charset != null)
        {
            operations.Add(new AlterTableOperation.SetDefaultCharacterSet(charset) { IncludeDefault = includeDefault, IncludeEquals = includeEquals });
        }
        if (collation != null)
        {
            operations.Add(new AlterTableOperation.SetDefaultCollation(collation) { IncludeDefault = includeDefault, IncludeEquals = includeEquals });
        }
        var statement = new AlterTable(new ObjectName([new Identifier("books")]),
            operations);

        using var formatter = Helpers.CreateFormatter(null);

        var expected = expectedOperation2 != null
            ? $"""
                ALTER TABLE `books`
                    {expectedOperation1},
                    {expectedOperation2};
                """
            : $"""
                ALTER TABLE `books`
                    {expectedOperation1};
                """;

        Assert.Equal(expected, formatter.GetFormatted(statement), ignoreLineEndingDifferences: true);
    }

    [Theory]
    [InlineData("This is a comment", "COMMENT = 'This is a comment'")]
    [InlineData(null, "COMMENT = ''")]
    [InlineData("", "COMMENT = ''")]
    public void AlterTable_FormatsComment(string? comment, string expectedOperation)
    {
        var statement = new AlterTable(new ObjectName([new Identifier("books")]),
            [new AlterTableOperation.TableComment(comment)]);
        using var formatter = Helpers.CreateFormatter(null);

        var expected = $"""
        ALTER TABLE `books`
            {expectedOperation};
        """;

        Assert.Equal(expected, formatter.GetFormatted(statement), ignoreLineEndingDifferences: true);
    }

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
