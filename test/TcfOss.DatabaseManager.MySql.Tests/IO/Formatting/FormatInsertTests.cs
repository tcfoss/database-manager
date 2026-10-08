namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatInsertTests
{
    [Fact]
    public void Insert_Values_NoColumns_NoPseudoTables()
    {
        var text = "INSERT INTO books VALUES ('My Book', 42)";
        var formatted = """
        INSERT INTO `books`
        VALUES
        (
            'My Book',
            42
        );
        """;

        using var formatter = Helpers.CreateFormatter(null, valueListMultiLineThreshold: 2);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Values_WithColumns_NoPseudoTables()
    {
        var text = "INSERT INTO books (title, published_year) VALUES ('My Book', 2024)";
        var formatted = """
        INSERT INTO `books`
        (
            `title`,
            `published_year`
        )
        VALUES
        (
            'My Book',
            2024
        );
        """;

        using var formatter = Helpers.CreateFormatter(null, valueListMultiLineThreshold: 2);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Values_MultipleRows_NoPseudoTables()
    {
        var text = "INSERT INTO books (title, published_year) VALUES ('Book One', 2023), ('Book Two', 2024)";
        var formatted = """
        INSERT INTO `books`
        (
            `title`,
            `published_year`
        )
        VALUES
        (
            'Book One',
            2023
        ),
        (
            'Book Two',
            2024
        );
        """;

        using var formatter = Helpers.CreateFormatter(null, valueListMultiLineThreshold: 2);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Values_OpeningParensOnNewLine_False()
    {
        // OpeningParensOnNewLine=false affects the column list paren.
        // With threshold=3 and only 2 values, VALUES is formatted single-line.
        var text = "INSERT INTO books (title, published_year) VALUES ('My Book', 2024)";
        var formatted = """
        INSERT INTO `books` (
            `title`,
            `published_year`
        )
        VALUES
        ('My Book', 2024);
        """;

        using var formatter = Helpers.CreateFormatter(null, openingParensOnNewLine: false, valueListMultiLineThreshold: 3);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Values_WithSchema_PseudoTables()
    {
        var text = "INSERT INTO books (title, author_id) VALUES ('My Book', 1)";
        var formatted = """
        INSERT INTO `schema1`.`books`
        (
            `title`,
            `author_id`
        )
        VALUES
        (
            'My Book',
            1
        );
        """;

        using var formatter = Helpers.CreateFormatter(Helpers.s_pseudoTables1, objectNamePrefixWithSchema: true, valueListMultiLineThreshold: 2);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Select_NoPseudoTables()
    {
        var text = "INSERT INTO books (title, published_year) SELECT title, published_year FROM other_books WHERE published_year > 2020";
        var formatted = """
        INSERT INTO `books`
        (
            `title`,
            `published_year`
        )
        SELECT
            `title`,
            `published_year`
        FROM `other_books`
        WHERE `published_year` > 2020;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Select_WithPseudoTables()
    {
        var text = "INSERT INTO books (title, author_id) SELECT name, id FROM authors";
        var formatted = """
        INSERT INTO `schema1`.`books`
        (
            `title`,
            `author_id`
        )
        SELECT
            `authors`.`name`,
            `authors`.`id`
        FROM `schema1`.`authors`;
        """;

        using var formatter = Helpers.CreateFormatter(Helpers.s_pseudoTables1, objectNamePrefixWithSchema: true);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Values_OnDuplicateKeyUpdate_NoPseudoTables()
    {
        var text = "INSERT INTO books (title, published_year) VALUES ('My Book', 2024) ON DUPLICATE KEY UPDATE published_year = 2024";
        var formatted = """
        INSERT INTO `books`
        (
            `title`,
            `published_year`
        )
        VALUES
        (
            'My Book',
            2024
        )
        ON DUPLICATE KEY UPDATE
            `published_year` = 2024;
        """;

        using var formatter = Helpers.CreateFormatter(null, valueListMultiLineThreshold: 2);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_Select_OnDuplicateKeyUpdate_MultipleAssignments_WithPseudoTables()
    {
        var text = "INSERT INTO books (title, author_id) SELECT name, id FROM authors ON DUPLICATE KEY UPDATE title = name, author_id = id";
        var formatted = """
        INSERT INTO `schema1`.`books`
        (
            `title`,
            `author_id`
        )
        SELECT
            `authors`.`name`,
            `authors`.`id`
        FROM `schema1`.`authors`
        ON DUPLICATE KEY UPDATE
            `title` = `name`,
            `author_id` = `id`;
        """;

        using var formatter = Helpers.CreateFormatter(Helpers.s_pseudoTables1, objectNamePrefixWithSchema: true);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_OnDuplicateKeyUpdate_TargetPrefix_True_SourcePrefix_True()
    {
        var text = "INSERT INTO books (title, author_id) VALUES ('My Book', 1) ON DUPLICATE KEY UPDATE title = title, author_id = author_id + 1";
        var formatted = """
        INSERT INTO `schema1`.`books`
        (
            `title`,
            `author_id`
        )
        VALUES
        (
            'My Book',
            1
        )
        ON DUPLICATE KEY UPDATE
            `books`.`title` = `books`.`title`,
            `books`.`author_id` = `books`.`author_id` + 1;
        """;

        using var formatter = Helpers.CreateFormatter(
            Helpers.s_pseudoTables1,
            objectNamePrefixWithSchema: true,
            joinConditionIndent: null,
            valueListMultiLineThreshold: 2,
            insertUpdateTargetPrefixWithObject: true,
            insertUpdateSourcePrefixWithObject: true
        );
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_OnDuplicateKeyUpdate_TargetPrefix_False_SourcePrefix_True()
    {
        // Default: target unprefixed, source (RHS) prefixed with target table when known.
        var text = "INSERT INTO books (title, author_id) VALUES ('My Book', 1) ON DUPLICATE KEY UPDATE title = title, author_id = author_id + 1";
        var formatted = """
        INSERT INTO `schema1`.`books`
        (
            `title`,
            `author_id`
        )
        VALUES
        (
            'My Book',
            1
        )
        ON DUPLICATE KEY UPDATE
            `title` = `books`.`title`,
            `author_id` = `books`.`author_id` + 1;
        """;

        using var formatter = Helpers.CreateFormatter(
            Helpers.s_pseudoTables1,
            objectNamePrefixWithSchema: true,
            joinConditionIndent: null,
            valueListMultiLineThreshold: 2,
            insertUpdateTargetPrefixWithObject: false,
            insertUpdateSourcePrefixWithObject: true
        );
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_OnDuplicateKeyUpdate_TargetPrefix_True_SourcePrefix_False()
    {
        var text = "INSERT INTO books (title, author_id) VALUES ('My Book', 1) ON DUPLICATE KEY UPDATE title = title, author_id = author_id + 1";
        var formatted = """
        INSERT INTO `schema1`.`books`
        (
            `title`,
            `author_id`
        )
        VALUES
        (
            'My Book',
            1
        )
        ON DUPLICATE KEY UPDATE
            `books`.`title` = `title`,
            `books`.`author_id` = `author_id` + 1;
        """;

        using var formatter = Helpers.CreateFormatter(
            Helpers.s_pseudoTables1,
            objectNamePrefixWithSchema: true,
            joinConditionIndent: null,
            valueListMultiLineThreshold: 2,
            insertUpdateTargetPrefixWithObject: true,
            insertUpdateSourcePrefixWithObject: false
        );
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_OnDuplicateKeyUpdate_TargetPrefix_False_SourcePrefix_False()
    {
        var text = "INSERT INTO books (title, author_id) VALUES ('My Book', 1) ON DUPLICATE KEY UPDATE title = title, author_id = author_id + 1";
        var formatted = """
        INSERT INTO `schema1`.`books`
        (
            `title`,
            `author_id`
        )
        VALUES
        (
            'My Book',
            1
        )
        ON DUPLICATE KEY UPDATE
            `title` = `title`,
            `author_id` = `author_id` + 1;
        """;

        using var formatter = Helpers.CreateFormatter(
            Helpers.s_pseudoTables1,
            objectNamePrefixWithSchema: true,
            joinConditionIndent: null,
            valueListMultiLineThreshold: 2,
            insertUpdateTargetPrefixWithObject: false,
            insertUpdateSourcePrefixWithObject: false
        );
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_OnDuplicateKeyUpdate_NoPseudoTables_NoQualificationApplied()
    {
        // Without a known target schema, both flags are no-ops: bare columns stay bare.
        var text = "INSERT INTO books (title) VALUES ('My Book') ON DUPLICATE KEY UPDATE title = title";
        var formatted = """
        INSERT INTO `books`
        (
            `title`
        )
        VALUES
        ('My Book')
        ON DUPLICATE KEY UPDATE
            `title` = `title`;
        """;

        using var formatter = Helpers.CreateFormatter(null, insertUpdateTargetPrefixWithObject: true, insertUpdateSourcePrefixWithObject: true);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Insert_LineCommentBefore()
    {
        // With threshold=2 and only 1 value, VALUES is formatted single-line.
        var text = "-- a comment\nINSERT INTO t VALUES (1); -- line after";
        var formatted = """
        -- a comment
        INSERT INTO `t`
        VALUES
        (1);  -- line after
        """;

        using var formatter = Helpers.CreateFormatter(null, valueListMultiLineThreshold: 2);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }
}
