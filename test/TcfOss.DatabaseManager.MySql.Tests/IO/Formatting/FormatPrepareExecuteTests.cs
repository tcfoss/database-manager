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

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Prepare_FromVariable()
    {
        var text = "PREPARE stmt FROM @sql_var";
        var formatted = """
        PREPARE `stmt` FROM @sql_var;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void DeallocatePrepare_Deallocate()
    {
        var text = "DEALLOCATE PREPARE stmt";
        var formatted = """
        DEALLOCATE PREPARE `stmt`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void DeallocatePrepare_Drop()
    {
        var text = "DROP PREPARE stmt";
        var formatted = """
        DROP PREPARE `stmt`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Execute_NoUsing()
    {
        var text = "EXECUTE stmt";
        var formatted = """
        EXECUTE `stmt`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Execute_WithUsing()
    {
        var text = "EXECUTE stmt USING @a, @b";
        var formatted = """
        EXECUTE `stmt` USING @a, @b;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }
}
