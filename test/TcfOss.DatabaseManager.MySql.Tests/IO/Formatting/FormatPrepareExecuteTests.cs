namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatPrepareExecuteTests
{
    [Fact]
    public void Prepare_FromString()
    {
        var text = "PREPARE stmt FROM 'SELECT 1'";
        var formatted = """
        PREPARE `stmt` FROM 'SELECT 1';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Prepare_FromVariable()
    {
        var text = "PREPARE stmt FROM @sql_var";
        var formatted = """
        PREPARE `stmt` FROM @sql_var;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeallocatePrepare_Deallocate()
    {
        var text = "DEALLOCATE PREPARE stmt";
        var formatted = """
        DEALLOCATE PREPARE `stmt`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void DeallocatePrepare_Drop()
    {
        var text = "DROP PREPARE stmt";
        var formatted = """
        DROP PREPARE `stmt`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Execute_NoUsing()
    {
        var text = "EXECUTE stmt";
        var formatted = """
        EXECUTE `stmt`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Execute_WithUsing()
    {
        var text = "EXECUTE stmt USING @a, @b";
        var formatted = """
        EXECUTE `stmt` USING @a, @b;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
