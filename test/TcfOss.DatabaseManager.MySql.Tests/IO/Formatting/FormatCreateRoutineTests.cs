namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatCreateRoutineTests
{
    [Fact]
    public void CreateProcedure_Simple()
    {
        var text = "CREATE PROCEDURE my_proc(p1 INT) BEGIN ROLLBACK; END";
        var formatted = """
        CREATE
        PROCEDURE `my_proc` (`p1` INT)
        BEGIN
            ROLLBACK;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateProcedure_MultiLineParams()
    {
        var text = "CREATE PROCEDURE my_proc(IN p1 INT, OUT p2 VARCHAR(100), INOUT p3 INT) BEGIN ROLLBACK; END";
        var formatted = """
        CREATE
        PROCEDURE `my_proc` (
            IN `p1` INT,
            OUT `p2` VARCHAR(100),
            INOUT `p3` INT
        )
        BEGIN
            ROLLBACK;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateProcedure_WithOrReplace_Definer()
    {
        var text = "CREATE OR REPLACE DEFINER = CURRENT_USER PROCEDURE my_proc(p1 INT) BEGIN ROLLBACK; END";
        var formatted = """
        CREATE OR REPLACE
            DEFINER = CURRENT_USER
        PROCEDURE `my_proc` (`p1` INT)
        BEGIN
            ROLLBACK;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateProcedure_WithCharacteristic()
    {
        var text = "CREATE PROCEDURE my_proc(p1 INT) DETERMINISTIC BEGIN ROLLBACK; END";
        var formatted = """
        CREATE
        PROCEDURE `my_proc` (`p1` INT)
            DETERMINISTIC
        BEGIN
            ROLLBACK;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateProcedure_WithIfNotExists()
    {
        var text = "CREATE PROCEDURE IF NOT EXISTS my_proc(p1 INT) BEGIN ROLLBACK; END";
        var formatted = """
        CREATE
        PROCEDURE `my_proc` (`p1` INT)
            IF NOT EXISTS
        BEGIN
            ROLLBACK;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateFunction_Simple()
    {
        var text = "CREATE FUNCTION sq(x INT) RETURNS INT RETURN 1";
        var formatted = """
        CREATE
        FUNCTION `sq` (`x` INT)
        RETURNS INT
            RETURN 1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateFunction_WithOrReplace_Definer_BeginEnd()
    {
        var text = "CREATE OR REPLACE DEFINER = CURRENT_USER FUNCTION my_func(p1 INT) RETURNS INT BEGIN RETURN 1; END";
        var formatted = """
        CREATE OR REPLACE
            DEFINER = CURRENT_USER
        FUNCTION `my_func` (`p1` INT)
        RETURNS INT
        BEGIN
            RETURN 1;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateFunction_MultiLineParams()
    {
        var text = "CREATE FUNCTION my_func(p1 INT, p2 INT, p3 INT) RETURNS INT BEGIN RETURN 1; END";
        var formatted = """
        CREATE
        FUNCTION `my_func` (
            `p1` INT,
            `p2` INT,
            `p3` INT
        )
        RETURNS INT
        BEGIN
            RETURN 1;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateFunction_ReturnBody()
    {
        var text = "CREATE FUNCTION identity_func(x INT) RETURNS INT RETURN x";
        var formatted = """
        CREATE
        FUNCTION `identity_func` (`x` INT)
        RETURNS INT
            RETURN `x`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateProcedure_MultiLineParams_ThresholdNotReached()
    {
        var text = "CREATE PROCEDURE my_proc(IN p1 INT, OUT p2 VARCHAR(100), INOUT p3 INT) BEGIN ROLLBACK; END";
        var formatted = """
        CREATE
        PROCEDURE `my_proc` (IN `p1` INT, OUT `p2` VARCHAR(100), INOUT `p3` INT)
        BEGIN
            ROLLBACK;
        END;
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.RoutineParameterMultiLineThreshold = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateFunction_MultiLineParams_ThresholdNotReached()
    {
        var text = "CREATE FUNCTION my_func(p1 INT, p2 INT, p3 INT) RETURNS INT BEGIN RETURN 1; END";
        var formatted = """
        CREATE
        FUNCTION `my_func` (`p1` INT, `p2` INT, `p3` INT)
        RETURNS INT
        BEGIN
            RETURN 1;
        END;
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.RoutineParameterMultiLineThreshold = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateFunction_MultiLineParams_ThresholdNull()
    {
        var text = "CREATE FUNCTION my_func(p1 INT, p2 INT, p3 INT) RETURNS INT BEGIN RETURN 1; END";
        var formatted = """
        CREATE
        FUNCTION `my_func` (`p1` INT, `p2` INT, `p3` INT)
        RETURNS INT
        BEGIN
            RETURN 1;
        END;
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.RoutineParameterMultiLineThreshold = null;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateProcedure_ComplexBody_WithPseudoTables()
    {
        var text = """
        CREATE PROCEDURE my_proc() BEGIN
        DECLARE EXIT HANDLER FOR SQLEXCEPTION ROLLBACK;
        -- select books
        SELECT title, author_id FROM books;
        /* insert a book */
        INSERT INTO books (title, author_id, published_year) VALUES ('New Book', 1, 2024);
        -- update a book
        UPDATE books SET title = 'Updated Title' WHERE author_id = 1;
        /* delete old books */
        DELETE FROM books WHERE published_year < 2000;
        END
        """;

        var formatted = """
        CREATE
        PROCEDURE `my_proc` ()
        BEGIN
            DECLARE EXIT HANDLER
                FOR SQLEXCEPTION
                ROLLBACK;
            -- select books
            SELECT
                `books`.`title`,
                `books`.`author_id`
            FROM `schema1`.`books`;
            /* insert a book */
            INSERT INTO `schema1`.`books`
            (
                `title`,
                `author_id`,
                `published_year`
            )
            VALUES
            (
                'New Book',
                1,
                2024
            );
            -- update a book
            UPDATE `schema1`.`books`
            SET
                `books`.`title` = 'Updated Title'
            WHERE `books`.`author_id` = 1;
            /* delete old books */
            DELETE
            FROM `schema1`.`books`
            WHERE `books`.`published_year` < 2000;
        END;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
