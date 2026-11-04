using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
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
                1)
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
        cte (col1) AS (
            SELECT
                1) FROM previous
        """;
        Assert.Equal(expectedFormatted, FormatComponent(body).ReplaceLineEndings("\n"));
        var cte = new CommonTableExpression([body with { From = null }, body with { Name = new Identifier("second"), From = null }], true);
        Assert.StartsWith("WITH RECURSIVE cte (col1) AS (", FormatComponent(cte));
        Assert.Contains(",\nsecond (col1) AS (", FormatComponent(cte).ReplaceLineEndings("\n"));
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

    [Fact]
    public void DataTypeReferences_FormatBuiltInAndNamedTypes()
    {
        DataTypeReference builtIn = new DataType.Int();
        var named = new DataTypeReference.Named(new ObjectName([new Identifier("app"), new Identifier("Mood", QuoteStyle.Ansi)]));

        Assert.Equal("INT", FormatComponent(builtIn));
        Assert.Equal("\"app\".\"Mood\"", FormatComponent(named));
        Assert.Equal("\"value\" \"app\".\"Mood\"", FormatComponent(new StatementColumn.Typed(new Identifier("value"), named, [])));
        Assert.Equal("\"input\" \"app\".\"Mood\"", FormatComponent(new StatementRoutineParameter.Directed(new Identifier("input"), named, null)));
    }

    [Fact]
    public void DirectedRoutineParameters_ConvertResolvedTypesToStatementReferences()
    {
        var dataType = new DataType.Int();
        var parameter = new RoutineParameter.Directed(new Identifier("value"), dataType, RoutineParameterDirection.InOut);
        StatementRoutineParameter.Directed statement = parameter.ToStatementParameter();

        Assert.Same(dataType, parameter.DataType);
        Assert.Same(dataType, Assert.IsType<DataTypeReference.BuiltIn>(statement.DataType).DataType);
        Assert.Equal(parameter.Name, statement.Name);
        Assert.Equal(parameter.Direction, statement.Direction);
        Assert.Equal("INOUT value INT", statement.ToSql());
        Assert.Equal("INOUT \"value\" INT", FormatComponent(statement));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "OUT")]
    [InlineData(false, "OUTPUT")]
    [InlineData(true, null)]
    [InlineData(true, "OUT")]
    [InlineData(true, "OUTPUT")]
    public void UndirectedRoutineParameters_ConversionPreservesDefaultAndOutput(bool hasDefault, string? output)
    {
        var dataType = new DataType.Int();
        var parameter = new RoutineParameter.Undirected(new Identifier("value", QuoteStyle.None, SigilKind.Variable), dataType)
        {
            DefaultValue = hasDefault ? new Value.Number("7", false) : null,
            Options = output == null ? null : new MsRoutineParameterOptions(Output: OutputKeyword.Parse(output)),
        };
        StatementRoutineParameter.Undirected statement = parameter.ToStatementParameter();

        Assert.Same(dataType, parameter.DataType);
        Assert.Same(dataType, Assert.IsType<DataTypeReference.BuiltIn>(statement.DataType).DataType);
        Assert.Equal(parameter.Name, statement.Name);
        Assert.Same(parameter.DefaultValue, statement.DefaultValue);
        Assert.Same(parameter.Options, statement.Options);
        Assert.Equal(parameter.Options?.Output, statement.Options?.Output);
        string expected = $"@value INT{(hasDefault ? " = 7" : "")}{(output == null ? "" : " " + output)}";
        Assert.Equal(expected, statement.ToSql());
        Assert.Equal(expected, FormatComponent(statement));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ParameterDefaults_UseInheritedMarkerWhenCopied(bool expressionDefault, bool useDefaultKeyword)
    {
        var value = new Value.Number("7", false);
        ParameterDefault original = expressionDefault
            ? new ParameterDefault.ExpressionDefault(new LiteralValue(value), !useDefaultKeyword)
            : new ParameterDefault.ValueDefault(value, !useDefaultKeyword);
        ParameterDefault copied = original with { UseDefaultKeyword = useDefaultKeyword };

        Assert.Equal(useDefaultKeyword ? " DEFAULT 7" : " = 7", copied.ToSql());
        Assert.Equal(copied.ToSql(), FormatComponent(copied));
        Assert.NotEqual(original.UseDefaultKeyword, copied.UseDefaultKeyword);
    }

    [Fact]
    public void GeneralRoutineParameter_FormatsSqlServerOptionsAroundDefault()
    {
        var parameter = new StatementRoutineParameter.General(
            new Identifier("items", QuoteStyle.None, SigilKind.Variable), new DataType.Int(), null,
            new ParameterDefault.ValueDefault(new Value.Number("1", false), false),
            new MsRoutineParameterOptions(true, true, OutputKeyword.Output, true));

        Assert.Equal("@items INT VARYING NULL = 1 OUTPUT READONLY", parameter.ToSql());
        Assert.Equal(parameter.ToSql(), FormatComponent(parameter));
    }

    [Fact]
    public void CreateType_FormatsNamesFieldsAndCollations()
    {
        var enumerated = new CreateType.Enumerated(new ObjectName([new Identifier("app"), new Identifier("mood")]), ["sad", "happy"]);
        Assert.Equal("""CREATE TYPE "app"."mood" AS ENUM ('sad', 'happy')""", FormatComponent(enumerated));

        var composite = new CreateType.Composite(new ObjectName([new Identifier("app"), new Identifier("address")]),
        [
            new CompositeTypeField(new Identifier("street"), new DataType.Text(), new ObjectName([new Identifier("app"), new Identifier("collation")])),
            new CompositeTypeField(new Identifier("postal"), new DataTypeReference.Named(new ObjectName([new Identifier("app"), new Identifier("postal_code")]))),
        ]);
        Assert.Equal("""CREATE TYPE "app"."address" AS ("street" TEXT COLLATE "app"."collation", "postal" "app"."postal_code")""", FormatComponent(composite));
    }

    [Theory]
    [InlineData("CREATE DOMAIN label AS TEXT", "CREATE DOMAIN \"label\" AS TEXT")]
    [InlineData("CREATE DOMAIN label TEXT NULL", "CREATE DOMAIN \"label\" TEXT NULL")]
    [InlineData("CREATE DOMAIN label AS TEXT NOT NULL", "CREATE DOMAIN \"label\" AS TEXT NOT NULL")]
    [InlineData("CREATE DOMAIN label AS TEXT CHECK (VALUE IS NOT NULL)", "CREATE DOMAIN \"label\" AS TEXT CHECK (VALUE IS NOT NULL)")]
    [InlineData("CREATE DOMAIN label AS TEXT CONSTRAINT optional NULL", "CREATE DOMAIN \"label\" AS TEXT CONSTRAINT \"optional\" NULL")]
    [InlineData("CREATE DOMAIN app.label AS app.base_type COLLATE app.collation DEFAULT 'a' CONSTRAINT required NOT NULL CONSTRAINT valid CHECK (VALUE <> '')", "CREATE DOMAIN \"app\".\"label\" AS \"app\".\"base_type\" COLLATE \"app\".\"collation\" DEFAULT 'a' CONSTRAINT \"required\" NOT NULL CONSTRAINT \"valid\" CHECK (VALUE <> '')")]
    public void CreateDomain_FormatsNamesDefaultsAndConstraints(string sql, string expected)
    {
        var parser = new Parsing.Parser();
        var lexer = new Lexing.GenericLexer();
        Statement statement = Assert.Single(parser.Parse(new Parsing.ParserState([.. lexer.Tokenize(sql)]), null));

        Assert.Equal(expected, FormatComponent(statement));
        Assert.IsType<CreateDomain>(Assert.Single(parser.Parse(new Parsing.ParserState([.. lexer.Tokenize(expected)]), null)));
    }

    [Fact]
    public void ArrayReferences_FormatElementNamesAndPreserveDeclaredBounds()
    {
        var array = new DataTypeReference.Array(new DataTypeReference.Named(new ObjectName([new Identifier("app"), new Identifier("mood")])),
        [
            new DataTypeReference.Array.Dimension(null),
            new DataTypeReference.Array.Dimension(3)
        ]);

        Assert.Equal("\"app\".\"mood\"[][3]", FormatComponent(array));
        var standard = new DataTypeReference.Array(new DataTypeReference.BuiltIn(new DataType.Int()),
            [new DataTypeReference.Array.Dimension(4)])
        {
            UseArrayKeyword = true
        };
        Assert.Equal("INT ARRAY[4]", FormatComponent(standard));
    }

    [Fact]
    public void CoreParser_AcceptsStandardArrayTypeSyntax()
    {
        var parser = new Parsing.Parser();
        var lexer = new Lexing.GenericLexer();
        Statement statement = Assert.Single(parser.Parse(new Parsing.ParserState([.. lexer.Tokenize("CREATE TYPE wrapper AS (values TEXT ARRAY)")]), null));

        var composite = Assert.IsType<CreateType.Composite>(statement);
        Assert.IsType<DataTypeReference.Array>(Assert.Single(composite.Attributes).DataType);
        Assert.Equal("CREATE TYPE wrapper AS (values TEXT ARRAY)", statement.ToSql());
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
}
