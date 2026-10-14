namespace TcfOss.DatabaseManager.MsSql.Tests.IO.Formatting;

public class FormatDdlTests
{
    [Fact]
    public void StoredProcedure_Basic()
    {
        using var formatter = Helpers.CreateFormatter(null);
        var text = """create procedure sp @a INT = 0, @b varchar(20) as select 1""";

        var formatted = formatter.GetFormatted(text);

        var expected = """
        CREATE
        PROCEDURE [sp] (@a INT = 0, @b VARCHAR(20))
        AS
            SELECT
                1;
        """;
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void StoredProcedure_WithOutput()
    {
        using var formatter = Helpers.CreateFormatter(null);
        var text = """create procedure sp @a INT = 0, @b varchar(20), @c int output as select 1""";

        var formatted = formatter.GetFormatted(text);

        var expected = """
        CREATE
        PROCEDURE [sp] (
            @a INT = 0,
            @b VARCHAR(20),
            @c INT OUTPUT
        )
        AS
            SELECT
                1;
        """;
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void StoredProcedure_WithOut()
    {
        using var formatter = Helpers.CreateFormatter(null);
        var text = """create procedure sp @a INT = 0, @b varchar(20), @c int out as select 1""";

        var formatted = formatter.GetFormatted(text);

        var expected = """
        CREATE
        PROCEDURE [sp] (
            @a INT = 0,
            @b VARCHAR(20),
            @c INT OUT
        )
        AS
            SELECT
                1;
        """;
        Assert.Equal(expected, formatted);
    }
}
