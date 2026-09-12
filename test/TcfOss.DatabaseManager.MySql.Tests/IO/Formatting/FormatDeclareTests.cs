namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatDeclareTests
{
    [Fact]
    public void DeclareCondition_ErrorCode()
    {
        var text = "DECLARE my_cond CONDITION FOR 1234";
        var formatted = """
        DECLARE `my_cond` CONDITION FOR 1234;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareCondition_SqlState()
    {
        var text = "DECLARE my_cond CONDITION FOR SQLSTATE '42000'";
        var formatted = """
        DECLARE `my_cond` CONDITION FOR SQLSTATE '42000';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareCondition_SqlStateWithValue()
    {
        var text = "DECLARE my_cond CONDITION FOR SQLSTATE VALUE '42000'";
        var formatted = """
        DECLARE `my_cond` CONDITION FOR SQLSTATE VALUE '42000';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareLocalVariable_Simple()
    {
        var text = "DECLARE x INT";
        var formatted = """
        DECLARE `x` INT;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareLocalVariable_WithDefault()
    {
        var text = "DECLARE x INT DEFAULT 5";
        var formatted = """
        DECLARE `x` INT DEFAULT 5;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareLocalVariable_MultipleNames()
    {
        var text = "DECLARE x, y VARCHAR(100)";
        var formatted = """
        DECLARE `x`, `y` VARCHAR(100);
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeclareLocalVariable_StringDefault()
    {
        var text = "DECLARE x VARCHAR(100) DEFAULT 'hello'";
        var formatted = """
        DECLARE `x` VARCHAR(100) DEFAULT 'hello';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
