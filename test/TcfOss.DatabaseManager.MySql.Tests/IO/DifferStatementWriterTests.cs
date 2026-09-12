using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.IO;

public class DifferStatementWriterTests
{
    private static (TextWriter, DifferStatementWriter) GetWriter(
        bool prefixWithSchema = true,
        bool omitModifiersIfDefault = true,
        bool preferRawText = false,
        bool useDelimiterAroundPrograms = true,
        bool useDelimiterAroundViews = false)
    {
        var settings = new DifferFormattingSettings
        {
            ObjectNamePrefixWithSchema = prefixWithSchema,
            OmitModifiersIfDefault = omitModifiersIfDefault,
            ProcedurePreferRawText = preferRawText,
            FunctionPreferRawText = preferRawText,
            TriggerPreferRawText = preferRawText,
            ViewPreferRawText = preferRawText,
            UseDelimiterAroundPrograms = useDelimiterAroundPrograms,
            UseDelimiterAroundViews = useDelimiterAroundViews,
        };
        var writer = new StringWriter();
        return (writer, new DifferStatementWriter(settings, writer));
    }

    private static Statement GetStatement(string sql)
    {
        var parser = new TextParser(new MyLexer(), new MyParser());
        return parser.ParseText(sql, "file.sql")[0];
    }

    private static (TextWriter, DifferStatementWriter) GetWriterFromSettings(DifferFormattingSettings settings)
    {
        var writer = new StringWriter();
        return (writer, new DifferStatementWriter(settings, writer));
    }

    private static readonly string s_delimitedTemplate = """
        DELIMITER //
        {0}//
        DELIMITER ;
        """;

    private static readonly string s_procedureHeader = """
        CREATE PROCEDURE myschema.myproc (IN param1 INT)
        """;

    private static readonly string s_procedureBody = """
        BEGIN
            -- Hello there
            SELECT * FROM mytable WHERE id = param1;
        END
        """;

    private static readonly string s_procedureBodyReduced = """
        BEGIN SELECT * FROM mytable WHERE id = param1; END
        """;

    private static readonly string s_functionHeader = """
        CREATE FUNCTION myschema.myfunc (param1 INT) RETURNS INT
        """;

    private static readonly string s_functionBody = """
        BEGIN
            -- Function body
            SELECT * FROM mytable WHERE id = param1;
        END
        """;

    private static readonly string s_functionBodyReduced = """
        BEGIN SELECT * FROM mytable WHERE id = param1; END
        """;

    private static readonly string s_triggerHeader = """
        CREATE TRIGGER myschema.mytrigger BEFORE INSERT ON myschema.mytable FOR EACH ROW
        """;

    private static readonly string s_triggerBody = """
        BEGIN
            -- Trigger body
            SELECT * FROM mytable WHERE id = NEW.id;
        END
        """;

    private static readonly string s_triggerBodyReduced = """
        BEGIN SELECT * FROM mytable WHERE id = NEW.id; END
        """;

    private static readonly string s_viewHeader = """
        CREATE VIEW myschema.myview
        """;

    private static readonly string s_viewBody = """
        SELECT * FROM mytable
        """;

    private static readonly string s_viewBodyReduced = """
        SELECT * FROM mytable
        """;

    private static readonly string s_eventHeader = """
        CREATE EVENT myschema.myevent ON SCHEDULE AT NOW() DO
        """;

    private static readonly string s_eventBody = """
        BEGIN
            -- Event body
            SELECT * FROM mytable;
        END
        """;

    [Fact]
    public void WriteCreateProcedure_NoPreferRawText_RawTextGiven_Formatted()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_procedureHeader}
            {s_procedureBody}
            """;
        var statement = (GetStatement(text) as CreateProcedure)!;
        statement.Meta.RawText = s_procedureBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_procedureHeader} {s_procedureBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateProcedure_NoPreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_procedureHeader}
            {s_procedureBody}
            """;
        var statement = (GetStatement(text) as CreateProcedure)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_procedureHeader} {s_procedureBodyReduced}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateProcedure_PreferRawText_RawTextGiven_NoSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_procedureHeader}
            {s_procedureBody}
            """;
        var statement = (GetStatement(text) as CreateProcedure)!;
        statement.Meta.RawText = s_procedureBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_procedureHeader}\n{s_procedureBody}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateProcedure_PreferRawText_RawTextGiven_WithSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_procedureHeader}
            {s_procedureBody}
            """;
        var statement = (GetStatement(text) as CreateProcedure)!;
        statement.Meta.RawText = $"\n{s_procedureBody}";

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_procedureHeader}\n{s_procedureBody}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateProcedure_PreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_procedureHeader}
            {s_procedureBody}
            """;
        var statement = (GetStatement(text) as CreateProcedure)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_procedureHeader} {s_procedureBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateFunction_NoPreferRawText_RawTextGiven_Formatted()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_functionHeader}
            {s_functionBody}
            """;
        var statement = (GetStatement(text) as CreateFunction)!;
        statement.Meta.RawText = s_functionBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_functionHeader} {s_functionBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateFunction_NoPreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_functionHeader}
            {s_functionBody}
            """;
        var statement = (GetStatement(text) as CreateFunction)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_functionHeader} {s_functionBodyReduced}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateFunction_PreferRawText_RawTextGiven_NoSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_functionHeader}
            {s_functionBody}
            """;
        var statement = (GetStatement(text) as CreateFunction)!;
        statement.Meta.RawText = s_functionBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_functionHeader}\n{s_functionBody}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateFunction_PreferRawText_RawTextGiven_WithSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_functionHeader}
            {s_functionBody}
            """;
        var statement = (GetStatement(text) as CreateFunction)!;
        statement.Meta.RawText = $"\n{s_functionBody}";

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_functionHeader}\n{s_functionBody}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateFunction_PreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_functionHeader}
            {s_functionBody}
            """;
        var statement = (GetStatement(text) as CreateFunction)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_functionHeader} {s_functionBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateTrigger_NoPreferRawText_RawTextGiven_Formatted()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_triggerHeader}
            {s_triggerBody}
            """;
        var statement = (GetStatement(text) as CreateTrigger)!;
        statement.Meta.RawText = s_triggerBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_triggerHeader} {s_triggerBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateTrigger_NoPreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_triggerHeader}
            {s_triggerBody}
            """;
        var statement = (GetStatement(text) as CreateTrigger)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_triggerHeader} {s_triggerBodyReduced}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateTrigger_PreferRawText_RawTextGiven_NoSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_triggerHeader}
            {s_triggerBody}
            """;
        var statement = (GetStatement(text) as CreateTrigger)!;
        statement.Meta.RawText = s_triggerBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_triggerHeader}\n{s_triggerBody}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateTrigger_PreferRawText_RawTextGiven_WithSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_triggerHeader}
            {s_triggerBody}
            """;
        var statement = (GetStatement(text) as CreateTrigger)!;
        statement.Meta.RawText = $"\n{s_triggerBody}";

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_triggerHeader}\n{s_triggerBody}"
        );
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateTrigger_PreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_triggerHeader}
            {s_triggerBody}
            """;
        var statement = (GetStatement(text) as CreateTrigger)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_triggerHeader} {s_triggerBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_NoPreferRawText_RawTextGiven_Formatted()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;
        statement.Meta.RawText = s_viewBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = $"{s_viewHeader} AS {s_viewBodyReduced};";
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_NoPreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = $"{s_viewHeader} AS {s_viewBodyReduced};";
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_PreferRawText_RawTextGiven_NoSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;
        statement.Meta.RawText = s_viewBody;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = $"{s_viewHeader}\nAS\n{s_viewBody};";
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_PreferRawText_RawTextGiven_WithSpace()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;
        statement.Meta.RawText = $"\n{s_viewBody}";

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = $"{s_viewHeader}\nAS\n{s_viewBody};";
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_PreferRawText_RawTextNotGiven()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: true);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = $"{s_viewHeader} AS {s_viewBodyReduced};";
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_UseDelimiterAroundViews_True_WrapsWithDelimiter()
    {
        var (writer, statementWriter) = GetWriter(preferRawText: false, useDelimiterAroundViews: true);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_viewHeader} AS {s_viewBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_UseDelimiterAroundViews_True_IsIndependentOfProgramDelimiterSetting()
    {
        var (writer, statementWriter) = GetWriter(
            preferRawText: false,
            useDelimiterAroundPrograms: false,
            useDelimiterAroundViews: true);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;

        statementWriter.WriteStatement(statement);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_viewHeader} AS {s_viewBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateProcedure_FromDeployScript_UsesProcedurePreferRawTextInScript()
    {
        var settings = new DifferFormattingSettings
        {
            ProcedurePreferRawText = false,
            ProcedurePreferRawTextInScript = true,
            UseDelimiterAroundPrograms = true,
        };
        var (writer, statementWriter) = GetWriterFromSettings(settings);
        var text = $"""
            {s_procedureHeader}
            {s_procedureBody}
            """;
        var statement = (GetStatement(text) as CreateProcedure)!;
        statement.Meta.RawText = s_procedureBody;

        statementWriter.WriteStatement(statement, fromDeployScript: true);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_procedureHeader}\n{s_procedureBody}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateProcedure_FromDeployScript_DoesNotUseNonScriptRawTextSetting()
    {
        var settings = new DifferFormattingSettings
        {
            ProcedurePreferRawText = true,
            ProcedurePreferRawTextInScript = false,
            UseDelimiterAroundPrograms = true,
        };
        var (writer, statementWriter) = GetWriterFromSettings(settings);
        var text = $"""
            {s_procedureHeader}
            {s_procedureBody}
            """;
        var statement = (GetStatement(text) as CreateProcedure)!;
        statement.Meta.RawText = s_procedureBody;

        statementWriter.WriteStatement(statement, fromDeployScript: true);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_procedureHeader} {s_procedureBodyReduced}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateFunction_FromDeployScript_UsesFunctionPreferRawTextInScript()
    {
        var settings = new DifferFormattingSettings
        {
            FunctionPreferRawText = false,
            FunctionPreferRawTextInScript = true,
            UseDelimiterAroundPrograms = true,
        };
        var (writer, statementWriter) = GetWriterFromSettings(settings);
        var text = $"""
            {s_functionHeader}
            {s_functionBody}
            """;
        var statement = (GetStatement(text) as CreateFunction)!;
        statement.Meta.RawText = s_functionBody;

        statementWriter.WriteStatement(statement, fromDeployScript: true);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_functionHeader}\n{s_functionBody}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateTrigger_FromDeployScript_UsesTriggerPreferRawTextInScript()
    {
        var settings = new DifferFormattingSettings
        {
            TriggerPreferRawText = false,
            TriggerPreferRawTextInScript = true,
            UseDelimiterAroundPrograms = true,
        };
        var (writer, statementWriter) = GetWriterFromSettings(settings);
        var text = $"""
            {s_triggerHeader}
            {s_triggerBody}
            """;
        var statement = (GetStatement(text) as CreateTrigger)!;
        statement.Meta.RawText = s_triggerBody;

        statementWriter.WriteStatement(statement, fromDeployScript: true);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_triggerHeader}\n{s_triggerBody}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateView_FromDeployScript_UsesViewPreferRawTextInScript()
    {
        var settings = new DifferFormattingSettings
        {
            ViewPreferRawText = false,
            ViewPreferRawTextInScript = true,
            UseDelimiterAroundViews = false,
        };
        var (writer, statementWriter) = GetWriterFromSettings(settings);
        var text = $"""
            {s_viewHeader}
            AS
            {s_viewBody}
            """;
        var statement = (GetStatement(text) as CreateView)!;
        statement.Meta.RawText = s_viewBody;

        statementWriter.WriteStatement(statement, fromDeployScript: true);
        var actual = writer.ToString()!;
        var expected = $"{s_viewHeader}\nAS\n{s_viewBody};";
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCreateEvent_FromDeployScript_UsesEventPreferRawTextInScript()
    {
        var settings = new DifferFormattingSettings
        {
            EventPreferRawText = false,
            EventPreferRawTextInScript = true,
            UseDelimiterAroundPrograms = true,
        };
        var (writer, statementWriter) = GetWriterFromSettings(settings);
        var text = $"""
            {s_eventHeader}
            {s_eventBody}
            """;
        var statement = (GetStatement(text) as CreateEvent)!;
        statement.Meta.RawText = s_eventBody;

        statementWriter.WriteStatement(statement, fromDeployScript: true);
        var actual = writer.ToString()!;
        var expected = string.Format(
            s_delimitedTemplate,
            $"{s_eventHeader[..^3]}\nDO\n{s_eventBody}");
        Assert.Equal(expected, actual.TrimEnd(), ignoreLineEndingDifferences: true);
    }
}
