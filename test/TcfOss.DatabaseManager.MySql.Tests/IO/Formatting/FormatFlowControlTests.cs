namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatFlowControlTests
{
    [Fact]
    public void Close_Simple()
    {
        var text = "CLOSE cur";
        var formatted = """
        CLOSE `cur`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Open_Simple()
    {
        var text = "OPEN cur";
        var formatted = """
        OPEN `cur`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Iterate_Simple()
    {
        var text = "ITERATE loop_label";
        var formatted = """
        ITERATE `loop_label`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Leave_Simple()
    {
        var text = "LEAVE loop_label";
        var formatted = """
        LEAVE `loop_label`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Return_Literal()
    {
        var text = "RETURN 1";
        var formatted = """
        RETURN 1;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Return_Null()
    {
        var text = "RETURN NULL";
        var formatted = """
        RETURN NULL;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Fetch_IntoSingleVar()
    {
        var text = "FETCH cur INTO x";
        var formatted = """
        FETCH `cur` INTO `x`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Fetch_FromCursor()
    {
        var text = "FETCH FROM cur INTO x, y";
        var formatted = """
        FETCH FROM `cur` INTO `x`, `y`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Fetch_NextFromCursor()
    {
        var text = "FETCH NEXT FROM cur INTO x";
        var formatted = """
        FETCH NEXT FROM `cur` INTO `x`;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void FetchGroupNextRow_Simple()
    {
        var text = "FETCH GROUP NEXT ROW";
        var formatted = """
        FETCH GROUP NEXT ROW;
        """;

        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(text);
        Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
    }
}
