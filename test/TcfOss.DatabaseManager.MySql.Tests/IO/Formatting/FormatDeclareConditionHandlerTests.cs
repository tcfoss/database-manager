namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatDeclareConditionHandlerTests
{
    [Fact]
    public void DeclareConditionHandler_SqlException()
    {
        var text = "DECLARE EXIT HANDLER FOR SQLEXCEPTION ROLLBACK";
        var formatted = """
        DECLARE EXIT HANDLER
            FOR SQLEXCEPTION
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
    public void DeclareConditionHandler_SqlWarning()
    {
        var text = "DECLARE CONTINUE HANDLER FOR SQLWARNING ROLLBACK";
        var formatted = """
        DECLARE CONTINUE HANDLER
            FOR SQLWARNING
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
    public void DeclareConditionHandler_NotFound()
    {
        var text = "DECLARE CONTINUE HANDLER FOR NOT FOUND ROLLBACK";
        var formatted = """
        DECLARE CONTINUE HANDLER
            FOR NOT FOUND
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
    public void DeclareConditionHandler_ErrorCode()
    {
        var text = "DECLARE CONTINUE HANDLER FOR 1234 ROLLBACK";
        var formatted = """
        DECLARE CONTINUE HANDLER
            FOR 1234
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
    public void DeclareConditionHandler_SqlState()
    {
        var text = "DECLARE CONTINUE HANDLER FOR SQLSTATE '42S02' ROLLBACK";
        var formatted = """
        DECLARE CONTINUE HANDLER
            FOR SQLSTATE '42S02'
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
    public void DeclareConditionHandler_ConditionName_Quoted()
    {
        var text = "DECLARE CONTINUE HANDLER FOR mycon ROLLBACK";
        var formatted = """
        DECLARE CONTINUE HANDLER
            FOR `mycon`
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
    public void DeclareConditionHandler_MultipleConditions()
    {
        var text = "DECLARE EXIT HANDLER FOR SQLEXCEPTION, NOT FOUND, 1234 ROLLBACK";
        var formatted = """
        DECLARE EXIT HANDLER
            FOR SQLEXCEPTION, NOT FOUND, 1234
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
    public void DeclareConditionHandler_BeginEnd_Handler()
    {
        var text = "DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END";
        var formatted = """
        DECLARE EXIT HANDLER
            FOR SQLEXCEPTION
        BEGIN
            ROLLBACK;
            RESIGNAL;
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
    public void DeclareConditionHandler_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nDECLARE EXIT HANDLER FOR SQLEXCEPTION RESIGNAL;\n\n/* block comment */\n";
        var formatted = """
        -- a comment
        DECLARE EXIT HANDLER
            FOR SQLEXCEPTION
            RESIGNAL;

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
