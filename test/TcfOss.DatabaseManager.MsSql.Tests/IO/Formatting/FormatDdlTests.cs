using TcfOss.DatabaseManager.Core.Configuration.Attributes;

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
        Assert.Equal(expected, formatted, ignoreLineEndingDifferences: true);
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
        Assert.Equal(expected, formatted, ignoreLineEndingDifferences: true);
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
        Assert.Equal(expected, formatted, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void CreateTable_WithComputedColumns_Default()
    {
        using var formatter = Helpers.CreateFormatter(null);
        var text = """CREATE TABLE T (A AS B - 2,X AS (Y + 1) PERSISTED NOT NULL)""";

        var formatted = formatter.GetFormatted(text);

        var expected = """
        CREATE TABLE [T]
        (
            [A] AS [B] - 2,
            [X] AS ([Y] + 1) PERSISTED NOT NULL
        );
        """;
        Assert.Equal(expected, formatted, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void CreateTable_WithComputedColumns_AlwaysNest()
    {
        using var formatter = Helpers.CreateFormatter(computedColumnParentheseHandling: MsComputedColumnParentheseHandling.Always);
        var text = """CREATE TABLE T (A AS B - 2,X AS (Y + 1) PERSISTED NOT NULL, C as D + 1 CONSTRAINT uc_t_c UNIQUE, E as F * 2 constraint ck_t_e check (E > 0))""";

        var formatted = formatter.GetFormatted(text);

        var expected = """
        CREATE TABLE [T]
        (
            [A] AS ([B] - 2),
            [X] AS ([Y] + 1) PERSISTED NOT NULL,
            [C] AS ([D] + 1) CONSTRAINT [uc_t_c] UNIQUE,
            [E] AS ([F] * 2) CONSTRAINT [ck_t_e] CHECK ([E] > 0)
        );
        """;
        Assert.Equal(expected, formatted, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void CreateTable_WithComputedColumns_NeverNest()
    {
        using var formatter = Helpers.CreateFormatter(computedColumnParentheseHandling: MsComputedColumnParentheseHandling.Never);
        var text = """CREATE TABLE T (A AS B - 2,X AS (Y + 1) persisted not null unique)""";

        var formatted = formatter.GetFormatted(text);

        var expected = """
        CREATE TABLE [T]
        (
            [A] AS [B] - 2,
            [X] AS [Y] + 1 PERSISTED NOT NULL UNIQUE
        );
        """;
        Assert.Equal(expected, formatted, ignoreLineEndingDifferences: true);
    }
}
