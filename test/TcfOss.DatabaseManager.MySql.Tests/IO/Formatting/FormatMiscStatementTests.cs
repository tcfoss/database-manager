using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatMiscStatementTests
{
    [Fact]
    public void DropObject_Table()
    {
        var text = "DROP TABLE books";
        var formatted = """
        DROP TABLE `books`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DropObject_TableIfExists()
    {
        var text = "DROP TABLE IF EXISTS books";
        var formatted = """
        DROP TABLE IF EXISTS `books`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DropObject_View()
    {
        var text = "DROP VIEW my_view";
        var formatted = """
        DROP VIEW `my_view`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DropObject_WithSchema()
    {
        var text = "DROP TABLE schema1.books";
        var formatted = """
        DROP TABLE `schema1`.`books`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Truncate_Simple()
    {
        var text = "TRUNCATE books";
        var formatted = """
        TRUNCATE `books`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Truncate_NoQuote()
    {
        var text = """
                TRUNCATE

        TABLE
        books
        """;
        var formatted = """
        TRUNCATE TABLE books;
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.Quoting = IdentifierQuotationHandling.IfSpecial;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Truncate_TableKeyword()
    {
        var text = "TRUNCATE TABLE books";
        var formatted = """
        TRUNCATE TABLE `books`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Use_Simple()
    {
        var text = "USE mydb";
        var formatted = """
        USE `mydb`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Use_WithSchemaKeyword()
    {
        var text = "USE SCHEMA mydb";
        var formatted = """
        USE SCHEMA `mydb`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void SetVariable_Single()
    {
        var text = "SET x = 1";
        var formatted = """
        SET `x` = 1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void SetVariable_Multiple()
    {
        var text = "SET x = 1, y = 2";
        var formatted = """
        SET `x` = 1, `y` = 2;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ShowDiagnostic_CountWarnings()
    {
        var text = "SHOW COUNT(*) WARNINGS";
        var formatted = """
        SHOW COUNT(*) WARNINGS;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ShowDiagnostic_CountErrors()
    {
        var text = "SHOW COUNT(*) ERRORS";
        var formatted = """
        SHOW COUNT(*) ERRORS;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ShowDiagnostic_Warnings()
    {
        var text = "SHOW WARNINGS";
        var formatted = """
        SHOW WARNINGS;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ShowDiagnostic_Errors()
    {
        var text = "SHOW ERRORS";
        var formatted = """
        SHOW ERRORS;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ShowDiagnostic_WarningsWithLimit()
    {
        var text = "SHOW WARNINGS LIMIT 10";
        var formatted = """
        SHOW WARNINGS LIMIT 10;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
