using System.Text;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;
using TcfOss.DatabaseManager.MsSql.Statements;

namespace TcfOss.DatabaseManager.MsSql.Tests.IO.Formatting;

public class FormatComponentsTests
{
    [Theory]
    [InlineData("declare @count int", "DECLARE @count INT;")]
    [InlineData("declare @count int = 1", "DECLARE @count INT = 1;")]
    [InlineData("declare @name nvarchar(50) = N'example'", "DECLARE @name NVARCHAR(50) = N'example';")]
    [InlineData("declare @count int = 1 + 2", "DECLARE @count INT = 1 + 2;")]
    public void VariableDeclaration(string input, string expected)
    {
        using var formatter = Helpers.CreateFormatter(null);
        var actual = formatter.GetFormatted(input);
        Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
    }

    [Theory]
    [InlineData("encryption", "WITH ENCRYPTION")]
    [InlineData("recompile", "WITH RECOMPILE")]
    [InlineData("execute as caller", "WITH EXECUTE AS CALLER")]
    [InlineData("execute as self", "WITH EXECUTE AS SELF")]
    [InlineData("execute as owner", "WITH EXECUTE AS OWNER")]
    [InlineData("execute as 'dbo'", "WITH EXECUTE AS 'dbo'")]
    [InlineData("encryption, recompile", "WITH ENCRYPTION, RECOMPILE")]
    [InlineData("encryption, execute as caller", "WITH ENCRYPTION, EXECUTE AS CALLER")]
    [InlineData("recompile, execute as caller", "WITH RECOMPILE, EXECUTE AS CALLER")]
    [InlineData("encryption, recompile, execute as caller", "WITH ENCRYPTION, RECOMPILE, EXECUTE AS CALLER")]
    public void RoutineWithOptions(string options, string expected)
    {
        var parser = new TextParser(new MsLexer(), new MsParser());
        var procedure = Assert.IsType<CreateProcedure>(Assert.Single(parser.ParseText($"CREATE PROCEDURE sp WITH {options} AS SELECT 1")));
        Assert.NotNull(procedure.MsOptions);

        var actual = FormatComponent(procedure.MsOptions);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, false, "WITH ENCRYPTION")]
    [InlineData(false, true, "WITH SCHEMABINDING")]
    [InlineData(true, true, "WITH ENCRYPTION, SCHEMABINDING")]
    public void ViewWithOptions(bool encryption, bool schemaBinding, string expected)
    {
        var options = new MsViewWithOptions
        {
            Encryption = encryption,
            SchemaBinding = schemaBinding,
        };

        var actual = FormatComponent(options);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData("@value OUTPUT", "@value OUTPUT")]
    [InlineData("@arg = 1", "@arg = 1")]
    [InlineData("@arg = @value OUTPUT", "@arg = @value OUTPUT")]
    [InlineData("DEFAULT", "DEFAULT")]
    [InlineData("DEFAULT OUTPUT", "DEFAULT OUTPUT")]
    [InlineData("@arg = DEFAULT", "@arg = DEFAULT")]
    [InlineData("@arg = DEFAULT OUTPUT", "@arg = DEFAULT OUTPUT")]
    public void ExecuteArgument(string argument, string expected)
    {
        var parser = new TextParser(new MsLexer(), new MsParser());
        var procedureCall = Assert.IsType<MsExecute.ProcedureCall>(Assert.Single(parser.ParseText($"EXEC dbo.MyProc {argument}")));
        Assert.NotNull(procedureCall.Arguments);

        var actual = FormatComponent(Assert.Single(procedureCall.Arguments));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("PRIMARY KEY (id)", "PRIMARY KEY ([id])")]
    [InlineData("PRIMARY KEY CLUSTERED (id)", "PRIMARY KEY CLUSTERED ([id])")]
    [InlineData("CONSTRAINT pk PRIMARY KEY NONCLUSTERED (id DESC)", "CONSTRAINT [pk] PRIMARY KEY NONCLUSTERED ([id] DESC)")]
    [InlineData("PRIMARY KEY (id) WITH (PAD_INDEX = ON, FILLFACTOR = 80)", "PRIMARY KEY ([id]) WITH (PAD_INDEX = ON, FILLFACTOR = 80)")]
    [InlineData("PRIMARY KEY (id) ON [PRIMARY]", "PRIMARY KEY ([id]) ON [PRIMARY]")]
    [InlineData("PRIMARY KEY NONCLUSTERED (id) WITH (ALLOW_ROW_LOCKS = OFF) ON [ps] ([id])", "PRIMARY KEY NONCLUSTERED ([id]) WITH (ALLOW_ROW_LOCKS = OFF) ON [ps] ([id])")]
    [InlineData("UNIQUE (id)", "UNIQUE ([id])")]
    [InlineData("UNIQUE CLUSTERED (id)", "UNIQUE CLUSTERED ([id])")]
    [InlineData("CONSTRAINT uq UNIQUE NONCLUSTERED (id)", "CONSTRAINT [uq] UNIQUE NONCLUSTERED ([id])")]
    [InlineData("UNIQUE (id) WITH (FILLFACTOR = 80)", "UNIQUE ([id]) WITH (FILLFACTOR = 80)")]
    [InlineData("UNIQUE (id) WITH (IGNORE_DUP_KEY = ON, ALLOW_PAGE_LOCKS = OFF) ON [PRIMARY]", "UNIQUE ([id]) WITH (IGNORE_DUP_KEY = ON, ALLOW_PAGE_LOCKS = OFF) ON [PRIMARY]")]
    [InlineData("UNIQUE NONCLUSTERED (id) INCLUDE (extra)", "UNIQUE NONCLUSTERED ([id]) INCLUDE ([extra])")]
    [InlineData("INDEX ix (id)", "INDEX [ix] ([id])")]
    [InlineData("INDEX ix CLUSTERED (id)", "INDEX [ix] CLUSTERED ([id])")]
    [InlineData("INDEX ix NONCLUSTERED (id)", "INDEX [ix] NONCLUSTERED ([id])")]
    [InlineData("INDEX ix (id) INCLUDE (extra)", "INDEX [ix] ([id]) INCLUDE ([extra])")]
    [InlineData("INDEX ix (id) WHERE id > 0", "INDEX [ix] ([id]) WHERE [id] > 0")]
    [InlineData("INDEX ix (id) WITH (STATISTICS_NORECOMPUTE = ON) ON [PRIMARY]", "INDEX [ix] ([id]) WITH (STATISTICS_NORECOMPUTE = ON) ON [PRIMARY]")]
    [InlineData("INDEX ix NONCLUSTERED (id DESC, extra ASC) INCLUDE (other) WHERE id > 0 WITH (FILLFACTOR = 90, PAD_INDEX = OFF) ON [ps] ([id])", "INDEX [ix] NONCLUSTERED ([id] DESC, [extra] ASC) INCLUDE ([other]) WHERE [id] > 0 WITH (FILLFACTOR = 90, PAD_INDEX = OFF) ON [ps] ([id])")]
    [InlineData("INDEX ix UNIQUE (id)", "INDEX [ix] UNIQUE ([id])")]
    [InlineData("INDEX ix UNIQUE CLUSTERED (id)", "INDEX [ix] UNIQUE CLUSTERED ([id])")]
    [InlineData("INDEX ix UNIQUE NONCLUSTERED (id)", "INDEX [ix] UNIQUE NONCLUSTERED ([id])")]
    [InlineData("INDEX ix UNIQUE (id) INCLUDE (extra)", "INDEX [ix] UNIQUE ([id]) INCLUDE ([extra])")]
    [InlineData("INDEX ix UNIQUE (id) WHERE id > 0", "INDEX [ix] UNIQUE ([id]) WHERE [id] > 0")]
    [InlineData("INDEX ix UNIQUE (id) WITH (FILLFACTOR = 80) ON [PRIMARY]", "INDEX [ix] UNIQUE ([id]) WITH (FILLFACTOR = 80) ON [PRIMARY]")]
    [InlineData("INDEX ix UNIQUE NONCLUSTERED (id DESC, extra ASC) INCLUDE (other) WHERE id > 0 WITH (FILLFACTOR = 90, PAD_INDEX = ON) ON [ps] ([id])", "INDEX [ix] UNIQUE NONCLUSTERED ([id] DESC, [extra] ASC) INCLUDE ([other]) WHERE [id] > 0 WITH (FILLFACTOR = 90, PAD_INDEX = ON) ON [ps] ([id])")]
    public void TableConstraint(string constraintSql, string expected)
    {
        var parser = new TextParser(new MsLexer(), new MsParser());
        var table = Assert.IsType<CreateTable>(Assert.Single(parser.ParseText($"CREATE TABLE t (id INT, extra INT, other INT, {constraintSql})")));
        var constraint = Assert.Single(table.Constraints);

        var actual = FormatComponent(constraint);
        Assert.Equal(expected, actual);

        using var writer = new SqlTextWriter(new StringBuilder());
        constraint.ToSql(writer);
        Assert.Equal(constraintSql, writer.ToString());
    }

    private static string FormatComponent(IWriteSql component)
    {
        var config = TestConfig.GetMsTestConfig();
        var functionNameProvider = new MsFunctionNameProvider();
        var indenter = new Indenter(config.Formatting.TabSize, config.Formatting.PreferTabs);
        var manager = new FormatManager
        {
            Formatting = config.Formatting,
            FunctionNameProvider = functionNameProvider,
            ComponentNormalizer = new ComponentNormalizer(config.QuoteStyle, functionNameProvider),
            Indenter = indenter,
        };
        using var writer = new SqlTextWriter(new StringBuilder(), indenter);

        component.FormatSql(writer, manager);
        return writer.ToString();
    }
}
