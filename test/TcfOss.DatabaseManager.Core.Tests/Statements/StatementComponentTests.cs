using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Tests.Statements;

public class StatementComponentTests
{
    [Theory]
    [InlineData(0, "()")]
    [InlineData(1, """("col0")""")]
    [InlineData(2, """("col0", "col1")""")]
    public void AssignmentTuple_FormatsAllNames(int count, string expected)
    {
        var target = new AssignmentTarget.Tuple([.. Enumerable.Range(0, count).Select(index => new ObjectName([new Identifier($"col{index}")]))]);

        Assert.Equal(expected, FormatComponent(target));
    }

    [Fact]
    public void FunctionArguments_None_WritesAndReferencesNothing()
    {
        var arguments = new FunctionArguments.None();

        Assert.Equal("", arguments.ToSql());
        Assert.Equal("", FormatComponent(arguments));
        Assert.Empty(arguments.GetReferencedItems(new ReferencedItemsManager()));
    }

    [Theory]
    [InlineData(false, false, """("col1")""")]
    [InlineData(true, false, """(DISTINCT "col1")""")]
    [InlineData(false, true, """("col1" LIMIT 5)""")]
    [InlineData(true, true, """(DISTINCT "col1" LIMIT 5)""")]
    public void FunctionArguments_List_FormatsDuplicateTreatmentAndClauses(bool distinct, bool clause, string expected)
    {
        var arguments = new FunctionArguments.List(
            [new FunctionArgument.Unnamed(new FunctionArgumentExpression.FunctionExpression(new SingleIdentifier(new Identifier("col1"))))],
            distinct ? DuplicateTreatment.Distinct : null,
            clause ? [new FunctionArgumentClause.Limit(new LiteralValue(new Value.Number("5", false)))] : null);

        Assert.Equal(expected, FormatComponent(arguments));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedTableFactor_WritesAndFormatsOptionalAlias(bool alias)
    {
        var factor = new TableFactor.NestedJoin
        {
            TableWithJoins = new TableWithJoins(new TableFactor.Table(new ObjectName([new Identifier("mytable")]))),
            Alias = alias ? new Identifier("nested") : null,
        };

        string aliasString = alias ? " AS nested" : "";

        Assert.Equal($"(mytable){aliasString}", factor.ToSql());
        string expectedFormatted = $"""
        (
            "mytable"
        ){aliasString}
        """;
        Assert.Equal(expectedFormatted, FormatComponent(factor).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void DerivedTableFactor_FormatsUnaliasedAst()
    {
        var query = new Select(new SelectBody.SimpleSelectQuery(new SimpleSelect([new SimpleSelectItem.UnnamedExpression(new LiteralValue(new Value.Number("1", false)))])));
        var factor = new TableFactor.Derived(query);

        string expectedFormatted = """
        (
            SELECT
                1
        )
        """;

        Assert.Equal(expectedFormatted, FormatComponent(factor).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void CommonTableExpression_FormatsColumnsAndSource()
    {
        var query = new Select(new SelectBody.SimpleSelectQuery(new SimpleSelect([new SimpleSelectItem.UnnamedExpression(new LiteralValue(new Value.Number("1", false)))])));
        var body = new CommonTableExpressionBody(new Identifier("cte"), [new Identifier("col1")], query, new Identifier("previous"));

        Assert.Equal("cte (col1) AS (SELECT 1) FROM previous", body.ToSql());
        string expectedFormatted = """
        "cte" (col1) AS (
            SELECT
                1) FROM previous
        """;
        Assert.Equal(expectedFormatted, FormatComponent(body).ReplaceLineEndings("\n"));
        var cte = new CommonTableExpression([body with { From = null }, body with { Name = new Identifier("second"), From = null }], true);
        Assert.StartsWith("WITH RECURSIVE \"cte\" (col1) AS (", FormatComponent(cte));
        Assert.Contains(",\n\"second\" (col1) AS (", FormatComponent(cte).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void AlterConstraintAst_FormatsNamedDrops()
    {
        Assert.Equal("DROP PRIMARY KEY \"pk\"", FormatComponent(new AlterTableOperation.DropPrimaryKey(new Identifier("pk"))));
        Assert.Equal("DROP CONSTRAINT \"uq\"", FormatComponent(new AlterTableOperation.DropUniqueKey(new Identifier("uq"))));
    }

    [Theory]
    [InlineData("/*short*/", "short")]
    [InlineData("/*first\nsecond*/", "first\nsecond")]
    public void BlockComment_FormatsDirectEntryPoint(string expected, string body)
    {
        Assert.Equal(expected, FormatComponent(new NonSql.BlockComment(body)).ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("\ttext", 4)]
    [InlineData(" \ttext", 4)]
    [InlineData("    \ttext", 8)]
    [InlineData("\t\ttext", 8)]
    public void NonSql_CountSpaces_UsesTabStops(string text, int expected)
    {
        Assert.Equal(expected, NonSql.CountSpacesBeforeText(text, 4));
    }

    private static string FormatComponent(IWriteSql component)
    {
        var functionNames = new FunctionNameProvider();
        var indenter = new Indenter(4, false);
        var manager = new FormatManager
        {
            Formatting = new FormattingSettings(),
            FunctionNameProvider = functionNames,
            ComponentNormalizer = new ComponentNormalizer(QuoteStyle.Ansi, functionNames),
            Indenter = indenter,
        };
        using var writer = new SqlTextWriter(new StringBuilder(), indenter);
        component.FormatSql(writer, manager);
        return writer.ToString();
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SetQuantifier>))]
    public void SetQuantifier_FromText(string text, SetQuantifier expected)
    {
        var result = SetQuantifier.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void SetQuantifier_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => SetQuantifier.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<CreateOrLabel>))]
    public void CreateOrLabel_FromText(string text, CreateOrLabel expected)
    {
        var result = CreateOrLabel.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void CreateOrLabel_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateOrLabel.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<HandlerAction>))]
    public void HandlerAction_FromText(string text, HandlerAction expected)
    {
        var result = HandlerAction.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void HandlerAction_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => HandlerAction.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<DuplicateTreatment>))]
    public void DuplicateTreatment_FromText(string text, DuplicateTreatment expected)
    {
        var result = DuplicateTreatment.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void DuplicateTreatment_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => DuplicateTreatment.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<MySqlTransactionCharacteristic>))]
    public void MySqlTransactionCharacteristic_FromText(string text, MySqlTransactionCharacteristic expected)
    {
        var result = MySqlTransactionCharacteristic.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void MySqlTransactionCharacteristic_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => MySqlTransactionCharacteristic.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SetOperator>))]
    public void SetOperator_FromText(string text, SetOperator expected)
    {
        var result = SetOperator.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void SetOperator_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => SetOperator.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<ShowDiagnosticType>))]
    public void ShowDiagnosticType_FromText(string text, ShowDiagnosticType expected)
    {
        var result = ShowDiagnosticType.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void ShowDiagnosticType_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => ShowDiagnosticType.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SignalPropertyName>))]
    public void SignalPropertyName_FromText(string text, SignalPropertyName expected)
    {
        var result = SignalPropertyName.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void SignalPropertyName_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => SignalPropertyName.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<TransactionNoun>))]
    public void TransactionNoun_FromText(string text, TransactionNoun expected)
    {
        var result = TransactionNoun.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void TransactionNoun_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => TransactionNoun.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<DeallocatePrepareLabel>))]
    public void DeallocatePrepareLabel_FromText(string text, DeallocatePrepareLabel expected)
    {
        var result = DeallocatePrepareLabel.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void DeallocatePrepareLabel_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => DeallocatePrepareLabel.Parse("UNKNOWN"));
    }
}
