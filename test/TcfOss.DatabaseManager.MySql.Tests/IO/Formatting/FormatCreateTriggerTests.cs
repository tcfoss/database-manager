namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatCreateTriggerTests
{
    [Fact]
    public void CreateTrigger_Simple_BeginEnd()
    {
        var text = "CREATE TRIGGER my_trigger BEFORE INSERT ON my_table FOR EACH ROW BEGIN ROLLBACK; END";
        var formatted = """
        CREATE
        TRIGGER `my_trigger`
        BEFORE INSERT ON `my_table`
        FOR EACH ROW
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
    public void CreateTrigger_Simple_SingleStatement()
    {
        var text = "CREATE TRIGGER my_trigger BEFORE DELETE ON my_table FOR EACH ROW ROLLBACK";
        var formatted = """
        CREATE
        TRIGGER `my_trigger`
        BEFORE DELETE ON `my_table`
        FOR EACH ROW
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTrigger_WithOrReplace_Definer()
    {
        var text = "CREATE OR REPLACE DEFINER = CURRENT_USER TRIGGER my_trigger AFTER UPDATE ON my_table FOR EACH ROW ROLLBACK";
        var formatted = """
        CREATE OR REPLACE
            DEFINER = CURRENT_USER
        TRIGGER `my_trigger`
        AFTER UPDATE ON `my_table`
        FOR EACH ROW
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTrigger_WithIfNotExists()
    {
        var text = "CREATE TRIGGER IF NOT EXISTS my_trigger BEFORE INSERT ON my_table FOR EACH ROW ROLLBACK";
        var formatted = """
        CREATE
        TRIGGER IF NOT EXISTS `my_trigger`
        BEFORE INSERT ON `my_table`
        FOR EACH ROW
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTrigger_WithFollows()
    {
        var text = "CREATE TRIGGER my_trigger BEFORE INSERT ON my_table FOR EACH ROW FOLLOWS other_trigger ROLLBACK";
        var formatted = """
        CREATE
        TRIGGER `my_trigger`
        BEFORE INSERT ON `my_table`
        FOR EACH ROW
            FOLLOWS `other_trigger`
            ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTrigger_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nCREATE TRIGGER my_trigger BEFORE INSERT ON my_table FOR EACH ROW ROLLBACK;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        CREATE
        TRIGGER `my_trigger`
        BEFORE INSERT ON `my_table`
        FOR EACH ROW
            ROLLBACK;

        /* block comment */
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
