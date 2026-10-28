using System.Text;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;
using TcfOss.DatabaseManager.MsSql.Statements;

namespace TcfOss.DatabaseManager.MsSql.Tests.FormattingTests;

public class FormatComponentsTests
{
    [Theory]
    [InlineData("TOP (5)", "TOP (5)")]
    [InlineData("TOP (@count) PERCENT WITH TIES", "TOP (@count) PERCENT WITH TIES")]
    public void Top_FormatsItsExpressionAndOptions(string clause, string expected)
    {
        var parser = new TextParser(new MsLexer(), new MsParser());
        var select = Assert.IsType<Select>(Assert.Single(parser.ParseText($"SELECT {clause} col1 FROM mytable ORDER BY col1")));
        var top = Assert.IsType<SelectBody.SimpleSelectQuery>(select.Body).Query.Top;
        Assert.NotNull(top);

        Assert.Equal(expected, FormatComponent(top));
    }

    [Theory]
    [InlineData(false, false, "()")]
    [InlineData(true, false, "(PARTITION BY col1)")]
    [InlineData(false, true, "(ORDER BY col2)")]
    [InlineData(true, true, "(PARTITION BY col1 ORDER BY col2)")]
    public void WindowSpec_FormatsOptionalClauses(bool partition, bool order, string expected)
    {
        var window = new WindowSpec
        {
            PartitionBy = partition ? [new SingleIdentifier(new Identifier("col1"))] : null,
            OrderBy = order ? [new OrderBy(new SingleIdentifier(new Identifier("col2")), null)] : null,
        };

        Assert.Equal(expected, FormatComponent(window));
    }

    [Fact]
    public void TableHint_UsesInheritedFormatter()
    {
        var parser = new TextParser(new MsLexer(), new MsParser());
        var select = Assert.IsType<Select>(Assert.Single(parser.ParseText("SELECT col1 FROM mytable WITH (NOLOCK)")));
        var from = Assert.IsType<SelectBody.SimpleSelectQuery>(select.Body).Query.From;
        Assert.NotNull(from);
        var hints = Assert.IsType<TableFactor.Table>(Assert.Single(from).Relation).Hints;
        Assert.NotNull(hints);

        Assert.Equal("NOLOCK", FormatComponent(Assert.Single(hints)));
    }

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
    [InlineData("PRIMARY KEY (id)", "PRIMARY KEY ([id])", Label = "primary key")]
    [InlineData("PRIMARY KEY CLUSTERED (id)", "PRIMARY KEY CLUSTERED ([id])", Label = "clustered primary key")]
    [InlineData("CONSTRAINT pk PRIMARY KEY NONCLUSTERED (id DESC)", "CONSTRAINT [pk] PRIMARY KEY NONCLUSTERED ([id] DESC)", Label = "named nonclustered primary key")]
    [InlineData("PRIMARY KEY (id) WITH (PAD_INDEX = ON, FILLFACTOR = 80)", "PRIMARY KEY ([id]) WITH (PAD_INDEX = ON, FILLFACTOR = 80)", Label = "primary key options")]
    [InlineData("PRIMARY KEY (id) ON [PRIMARY]", "PRIMARY KEY ([id]) ON [PRIMARY]", Label = "primary key filegroup")]
    [InlineData("PRIMARY KEY NONCLUSTERED (id) WITH (ALLOW_ROW_LOCKS = OFF) ON [ps] ([id])", "PRIMARY KEY NONCLUSTERED ([id]) WITH (ALLOW_ROW_LOCKS = OFF) ON [ps] ([id])", Label = "primary key partition scheme")]
    public void TableConstraint_PrimaryKey_TextMatch(string constraintSql, string expected)
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

    [Theory]
    [InlineData("UNIQUE (id)", "UNIQUE ([id])", Label = "unique")]
    [InlineData("UNIQUE CLUSTERED (id)", "UNIQUE CLUSTERED ([id])", Label = "clustered unique")]
    [InlineData("CONSTRAINT uq UNIQUE NONCLUSTERED (id)", "CONSTRAINT [uq] UNIQUE NONCLUSTERED ([id])", Label = "named nonclustered unique")]
    [InlineData("UNIQUE (id) WITH (FILLFACTOR = 80)", "UNIQUE ([id]) WITH (FILLFACTOR = 80)", Label = "unique options")]
    [InlineData("UNIQUE (id) WITH (IGNORE_DUP_KEY = ON, ALLOW_PAGE_LOCKS = OFF) ON [PRIMARY]", "UNIQUE ([id]) WITH (IGNORE_DUP_KEY = ON, ALLOW_PAGE_LOCKS = OFF) ON [PRIMARY]", Label = "unique options filegroup")]
    [InlineData("UNIQUE NONCLUSTERED (id) INCLUDE (extra)", "UNIQUE NONCLUSTERED ([id]) INCLUDE ([extra])", Label = "unique include")]
    public void TableConstraint_Unique_TextMatch(string constraintSql, string expected)
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

    [Theory]
    [InlineData("INDEX ix (id)", "INDEX [ix] ([id])", Label = "index")]
    [InlineData("INDEX ix CLUSTERED (id)", "INDEX [ix] CLUSTERED ([id])", Label = "clustered index")]
    [InlineData("INDEX ix NONCLUSTERED (id)", "INDEX [ix] NONCLUSTERED ([id])", Label = "nonclustered index")]
    [InlineData("INDEX ix (id) INCLUDE (extra)", "INDEX [ix] ([id]) INCLUDE ([extra])", Label = "index include")]
    [InlineData("INDEX ix (id) WHERE id > 0", "INDEX [ix] ([id]) WHERE [id] > 0", Label = "filtered index")]
    [InlineData("INDEX ix (id) WITH (STATISTICS_NORECOMPUTE = ON) ON [PRIMARY]", "INDEX [ix] ([id]) WITH (STATISTICS_NORECOMPUTE = ON) ON [PRIMARY]", Label = "index options filegroup")]
    [InlineData("INDEX ix NONCLUSTERED (id DESC, extra ASC) INCLUDE (other) WHERE id > 0 WITH (FILLFACTOR = 90, PAD_INDEX = OFF) ON [ps] ([id])", "INDEX [ix] NONCLUSTERED ([id] DESC, [extra] ASC) INCLUDE ([other]) WHERE [id] > 0 WITH (FILLFACTOR = 90, PAD_INDEX = OFF) ON [ps] ([id])", Label = "index options partition scheme")]
    public void TableConstraint_Index_TextMatch(string constraintSql, string expected)
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

    [Theory]
    [InlineData("INDEX ix UNIQUE (id)", "INDEX [ix] UNIQUE ([id])", Label = "unique index")]
    [InlineData("INDEX ix UNIQUE CLUSTERED (id)", "INDEX [ix] UNIQUE CLUSTERED ([id])", Label = "clustered unique index")]
    [InlineData("INDEX ix UNIQUE NONCLUSTERED (id)", "INDEX [ix] UNIQUE NONCLUSTERED ([id])", Label = "nonclustered unique index")]
    [InlineData("INDEX ix UNIQUE (id) INCLUDE (extra)", "INDEX [ix] UNIQUE ([id]) INCLUDE ([extra])", Label = "unique index include")]
    [InlineData("INDEX ix UNIQUE (id) WHERE id > 0", "INDEX [ix] UNIQUE ([id]) WHERE [id] > 0", Label = "filtered unique index")]
    [InlineData("INDEX ix UNIQUE (id) WITH (FILLFACTOR = 80) ON [PRIMARY]", "INDEX [ix] UNIQUE ([id]) WITH (FILLFACTOR = 80) ON [PRIMARY]", Label = "unique index options filegroup")]
    [InlineData("INDEX ix UNIQUE NONCLUSTERED (id DESC, extra ASC) INCLUDE (other) WHERE id > 0 WITH (FILLFACTOR = 90, PAD_INDEX = ON) ON [ps] ([id])", "INDEX [ix] UNIQUE NONCLUSTERED ([id] DESC, [extra] ASC) INCLUDE ([other]) WHERE [id] > 0 WITH (FILLFACTOR = 90, PAD_INDEX = ON) ON [ps] ([id])", Label = "unique index options partition scheme")]
    public void TableConstraint_UniqueIndex_TextMatch(string constraintSql, string expected)
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
