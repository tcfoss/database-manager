namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatCreateViewTests
{
    [Fact]
    public void CreateView_Simple()
    {
        var text = "CREATE VIEW my_view AS SELECT 1";
        var formatted = """
        CREATE
        VIEW `my_view`
        AS
        SELECT
            1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateView_WithOrReplace_Definer()
    {
        var text = "CREATE OR REPLACE DEFINER = CURRENT_USER VIEW my_view AS SELECT 1";
        var formatted = """
        CREATE OR REPLACE
            DEFINER = CURRENT_USER
        VIEW `my_view`
        AS
        SELECT
            1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateView_WithAlgorithm()
    {
        var text = "CREATE ALGORITHM = MERGE VIEW my_view AS SELECT 1";
        var formatted = """
        CREATE
            ALGORITHM = MERGE
        VIEW `my_view`
        AS
        SELECT
            1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateView_WithColumns()
    {
        var text = "CREATE VIEW my_view (a, b) AS SELECT 1, 2";
        var formatted = """
        CREATE
        VIEW `my_view` (`a`, `b`)
        AS
        SELECT
            1,
            2;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateView_WithSecurityContext()
    {
        var text = "CREATE SQL SECURITY INVOKER VIEW my_view AS SELECT 1";
        var formatted = """
        CREATE
            SQL SECURITY INVOKER
        VIEW `my_view`
        AS
        SELECT
            1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateView_WithCheckOption()
    {
        var text = "CREATE VIEW my_view AS SELECT 1 WITH CASCADED CHECK OPTION";
        var formatted = """
        CREATE
        VIEW `my_view`
        AS
        SELECT
            1
        WITH CASCADED CHECK OPTION;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateView_WithAllModifiers()
    {
        var text = "CREATE OR REPLACE ALGORITHM = TEMPTABLE DEFINER = CURRENT_USER SQL SECURITY INVOKER VIEW my_view AS SELECT 1";
        var formatted = """
        CREATE OR REPLACE
            ALGORITHM = TEMPTABLE
            DEFINER = CURRENT_USER
            SQL SECURITY INVOKER
        VIEW `my_view`
        AS
        SELECT
            1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateView_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nCREATE VIEW my_view AS SELECT 1;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        CREATE
        VIEW `my_view`
        AS
        SELECT
            1;

        /* block comment */
        """;

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
