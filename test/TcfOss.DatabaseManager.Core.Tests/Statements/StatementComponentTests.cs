using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;
using DuplicateTreatment = TcfOss.DatabaseManager.Core.Expressions.Attributes.DuplicateTreatment;

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
    [InlineData(false)]
    [InlineData(true)]
    public void Call_NonInvocationExpression_DelegatesFormattingAndReferences(bool hasReference)
    {
        Expression expression = hasReference
            ? new SingleIdentifier(new Identifier("target"))
            : new LiteralValue(new Value.Number("1", false));
        var call = new Call(expression);

        Assert.Equal(hasReference ? "CALL \"target\"" : "CALL 1", FormatComponent(call));
        ItemRef[] references = [.. call.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];
        Assert.Equal(hasReference ? 1 : 0, references.Length);
        if (hasReference)
        {
            Assert.Equal("target", Assert.Single(Assert.Single(references).Identifiers).Name);
        }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectedRoutineParameters_ConvertResolvedTypesToStatementReferences(bool hasDefault)
    {
        var dataType = new DataType.Int();
        var parameter = new RoutineParameter.Directed(new Identifier("value"), dataType, RoutineParameterDirection.InOut)
        {
            DefaultExpression = hasDefault ? new LiteralValue(new Value.Number("7", false)) : null
        };
        StatementRoutineParameter.Directed statement = parameter.ToStatementParameter();

        Assert.Same(dataType, parameter.DataType);
        Assert.Same(dataType, Assert.IsType<DataTypeReference.BuiltIn>(statement.DataType).DataType);
        Assert.Equal(parameter.Name, statement.Name);
        Assert.Equal(parameter.Direction, statement.Direction);
        Assert.Equal($"INOUT value INT{(hasDefault ? " DEFAULT 7" : "")}", statement.ToSql());
        Assert.Equal($"INOUT \"value\" INT{(hasDefault ? " DEFAULT 7" : "")}", FormatComponent(statement));
        if (hasDefault)
        {
            Assert.Same(parameter.DefaultExpression, Assert.IsType<ParameterDefault.ExpressionDefault>(statement.ParameterDefault).Expression);
        }
        else
        {
            Assert.Null(statement.ParameterDefault);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PostgresRoutineParameters_ConversionPreservesDirectionAndDefault(bool isInput, bool hasDefault)
    {
        var dataType = new DataType.Int();
        var parameter = new RoutineParameter.Postgres(new Identifier("value"), dataType,
            isInput ? RoutineParameterDirection.In : RoutineParameterDirection.InOut,
            hasDefault ? new LiteralValue(new Value.Number("7", false)) : null);

        StatementRoutineParameter.General statement = parameter.ToStatementParameter();

        Assert.Same(dataType, Assert.IsType<DataTypeReference.BuiltIn>(statement.DataType).DataType);
        Assert.Equal(parameter.Name, statement.Name);
        Assert.Equal(isInput ? null : RoutineParameterDirection.InOut, statement.Direction);
        Assert.Equal($"{(isInput ? "" : "INOUT ")}value INT{(hasDefault ? " = 7" : "")}", statement.ToSql());
        Assert.Equal($"{(isInput ? "" : "INOUT ")}\"value\" INT{(hasDefault ? " = 7" : "")}", FormatComponent(statement));
        if (hasDefault)
        {
            var statementDefault = Assert.IsType<ParameterDefault.ExpressionDefault>(statement.ParameterDefault);
            Assert.Same(parameter.DefaultExpression, statementDefault.Expression);
            Assert.False(statementDefault.UseDefaultKeyword);
        }
        else
        {
            Assert.Null(statement.ParameterDefault);
        }
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
        Assert.Same(parameter.DefaultValue, statement.ParameterDefault?.Value);
        if (hasDefault)
        {
            Assert.False(statement.ParameterDefault!.UseDefaultKeyword);
        }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UndirectedRoutineParameter_UsesEqualsRegardlessOfDefaultMarker(bool useDefaultKeyword)
    {
        var parameterDefault = new ParameterDefault.ValueDefault(new Value.Number("7", false), useDefaultKeyword);
        var parameter = new StatementRoutineParameter.Undirected(
            new Identifier("value", QuoteStyle.None, SigilKind.Variable), new DataType.Int(), parameterDefault);
        StatementRoutineParameter common = parameter;

        Assert.Same(parameterDefault, common.ParameterDefault);
        Assert.Null(common.Direction);
        Assert.Equal("@value INT = 7", parameter.ToSql());
        Assert.Equal("@value INT = 7", FormatComponent(parameter));
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

    [Theory]
    [InlineData("@>")]
    [InlineData("<->")]
    [InlineData("+")]
    [InlineData("@-")]
    public void PostgresOperatorReferences_EmitEscapedSchemaNames(string symbol)
    {
        var reference = new PgOperatorReference(symbol) { Schema = new Identifier("ops; DROP TABLE items; --", QuoteStyle.Ansi) };

        Assert.Equal("OPERATOR(\"ops; DROP TABLE items; --\"." + symbol + ")", reference.ToSql());
        Assert.Equal(reference.ToSql(), FormatComponent(reference));
        Assert.Equal(symbol, new PgOperatorReference(symbol).ToSql());
        Assert.Equal("OPERATOR(" + symbol + ")", new PgOperatorReference(symbol) { UseOperatorSyntax = true }.ToSql());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("name")]
    [InlineData("@>; SELECT 1")]
    [InlineData("--")]
    [InlineData("/*")]
    [InlineData("*- ")]
    [InlineData("*-")]
    [InlineData("=+")]
    public void PostgresOperatorReferences_RejectInvalidOrSqlBearingSymbols(string? symbol)
    {
        Assert.Throws<ArgumentException>(() => new PgOperatorReference(symbol!));
    }

    [Theory]
    [InlineData("!", " ESCAPE '!'")]
    [InlineData("'", " ESCAPE ''''")]
    [InlineData("", " ESCAPE ''")]
    public void PatternEscapeCharacter_WritesQuotedValueAndHasNoReferences(string value, string expected)
    {
        var escape = new PatternEscape.Character(value);

        Assert.Equal(expected, escape.ToSql());
        Assert.Equal(expected, FormatComponent(escape));
        Assert.Empty(escape.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Fact]
    public void PatternEscapeVariants_PreserveJsonAndExpressionReferences()
    {
        var expression = new SingleIdentifier(new Identifier("escape_value"));
        PatternEscape[] escapes = [new PatternEscape.Character("!"), new PatternEscape.Expression(expression)];
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };

        Assert.Equal(escapes, Serialization.FromJson<PatternEscape[]>(Serialization.ToJson(escapes)));
        Assert.Equal(expression.GetReferencedItems(context), escapes[1].GetReferencedItems(context));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Limit_AllVariants_ExposeExpressionReferences(int kind)
    {
        var count = new SingleIdentifier(new Identifier("count_value"));
        var offset = new SingleIdentifier(new Identifier("offset_value"));
        Limit limit = kind switch
        {
            0 => new Limit.ExpressionLimit(count),
            1 => new Limit.MyCommaSeparated(count, offset),
            2 => new Limit.MyLimitOffset(count, offset),
            3 => new Limit.OffsetFetch(offset, count),
            4 => new Limit.PgLimit(count, offset),
            5 => new Limit.PgOffset(offset),
            _ => new Limit.PgFetch(count, offset)
        };

        string[] names = [.. limit.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .SelectMany(reference => reference.Identifiers).Select(identifier => identifier.Name).Order()];
        string[] expected = kind switch
        {
            0 => ["count_value"],
            5 => ["offset_value"],
            _ => ["count_value", "offset_value"]
        };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void WindowAndOrderedAggregate_FormatMultipleOrderingKeys()
    {
        SqlValueList<OrderBy> ordering =
        [
            new(new SingleIdentifier(new Identifier("first_key")), null),
            new(new SingleIdentifier(new Identifier("second_key")), null)
        ];
        var window = new WindowSpec.InlineSpecification { OrderBy = ordering };
        var aggregate = new FunctionCall(new ObjectName([new Identifier("ordered_value")]))
        {
            Arguments = FunctionArguments.EmptyList(),
            WithinGroup = ordering
        };

        Assert.Equal("(ORDER BY \"first_key\", \"second_key\")", FormatComponent(window));
        Assert.Equal("\"ordered_value\"() WITHIN GROUP (ORDER BY \"first_key\", \"second_key\")", FormatComponent(aggregate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ConflictTargets_ExposeOnlyExpressionReferences(int predicateKind)
    {
        Expression? predicate = predicateKind switch
        {
            0 => null,
            1 => new LiteralValue(new Value.Boolean(true)),
            _ => new SingleIdentifier(new Identifier("predicate_value"))
        };
        var target = new PgConflictTarget.IndexInference([new PgConflictIndexElement(new SingleIdentifier(new Identifier("index_value")))], predicate);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        string[] expected = predicateKind == 2 ? ["index_value", "predicate_value"] : ["index_value"];

        Assert.Equal(expected, target.GetReferencedItems(context).SelectMany(reference => reference.Identifiers).Select(identifier => identifier.Name));
        Assert.Equal(expected, new PgOnConflict.DoNothing(target).GetReferencedItems(context).SelectMany(reference => reference.Identifiers).Select(identifier => identifier.Name));
        var update = new PgOnConflict.DoUpdate(target, [], predicate);
        string[] updateExpected = predicateKind == 2 ? ["index_value", "predicate_value", "predicate_value"] : ["index_value"];
        Assert.Equal(updateExpected, update.GetReferencedItems(context).SelectMany(reference => reference.Identifiers).Select(identifier => identifier.Name));
        Assert.Empty(new PgOnConflict.DoNothing().GetReferencedItems(context));
    }

    [Fact]
    public void ConflictConstraint_FormatsIdentifierWithoutObjectReference()
    {
        var target = new PgConflictTarget.Constraint(new Identifier("unique_key"));

        Assert.Equal("ON CONSTRAINT \"unique_key\"", FormatComponent(target));
        Assert.Empty(target.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RowLocking_FormatsOptionalTargetsAndWait(bool withOptions)
    {
        var locking = new RowLockingClause(RowLockStrength.Parse("UPDATE"))
        {
            Of = withOptions ? [new Identifier("items")] : null,
            Wait = withOptions ? RowLockWait.Parse("NOWAIT") : null
        };

        Assert.Equal(withOptions ? "FOR UPDATE OF \"items\" NOWAIT" : "FOR UPDATE", FormatComponent(locking));
    }

    [Fact]
    public void CteCycle_FormatsWithoutExplicitMarkValues()
    {
        var cycle = new PgCteCycle([new Identifier("id")], new Identifier("is_cycle"), new Identifier("path"));

        Assert.Equal("CYCLE \"id\" SET \"is_cycle\" USING \"path\"", FormatComponent(cycle));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CteReferences_RespectRecursiveVisibilityAndRestoreTableContext(bool recursive, bool withPseudoTables)
    {
        string sql = $"WITH {(recursive ? "RECURSIVE " : "")}results (id) AS (SELECT 1 AS id{(recursive ? " UNION ALL SELECT id FROM results" : "")}) SELECT results.id, LENGTH('x') AS size FROM results";
        var parser = new TextParser(new GenericLexer(), new Parser());
        var statement = Assert.IsType<Select>(Assert.Single(parser.ParseText(sql)));
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var pseudoTables = withPseudoTables ? new PseudoTableSet(schema, []) : null;
        var context = new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.All,
            FunctionNameProvider = new FunctionNameProvider(),
            PseudoTables = pseudoTables,
            ActiveSchema = schema
        };

        ItemRef[] references = [.. statement.GetReferencedItems(context)];

        var function = Assert.Single(references.OfType<ItemRef.ObjectNameRef>());
        Assert.Equal(ItemType.Unknown, function.Type);
        Assert.Equal("LENGTH", Assert.Single(function.Identifiers).Name);
        Assert.Same(pseudoTables, context.PseudoTables);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TableFunctions_RegisterAliasedColumnsWithoutInferringUnboundColumns(bool rowsFrom, bool withAlias)
    {
        string relation = rowsFrom ? "ROWS FROM (app.records() AS (first INT, second INT))" : "app.records()";
        var parser = new TextParser(new GenericLexer(), new Parser());
        var statement = Assert.IsType<Select>(Assert.Single(parser.ParseText($"SELECT 1 FROM {relation}{(withAlias ? " AS mapped (first, second)" : "")}")));
        var query = Assert.IsType<SelectBody.SimpleSelectQuery>(statement.Body);
        TableFactor factor = Assert.Single(query.Query.From!).Relation;
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var pseudoTables = new PseudoTableSet(schema, []);
        pseudoTables.EnterSelectScope();

        factor.AddTablesToContext(pseudoTables);

        string sourceName = withAlias ? "mapped" : "records";
        if (withAlias)
        {
            Assert.Single(pseudoTables.GetSelectableElements("first", sourceName));
            Assert.Equal(new[] { "first", "second" }, pseudoTables.GetLocalWildcardColumns(sourceName).Select(column => column.Name));
        }
        else
        {
            Assert.Empty(pseudoTables.GetSelectableElements("first", sourceName));
            Assert.Empty(pseudoTables.GetLocalWildcardColumns());
        }
        string formatted = FormatComponent(factor);
        if (rowsFrom)
        {
            Assert.Contains("AS (\"first\" INT, \"second\" INT)", formatted);
        }
        Assert.DoesNotContain("WITH ORDINALITY", formatted);
        pseudoTables.LeaveSelectScope();
    }

    [Theory]
    [InlineData("SELECT 1 AS id", false, "id")]
    [InlineData("SELECT 1 AS id", true, "id")]
    [InlineData("VALUES (1, 2)", false, "column1,column2")]
    [InlineData("INSERT INTO items DEFAULT VALUES RETURNING id", false, "id")]
    [InlineData("UPDATE items SET id = 1 RETURNING id", false, "id")]
    [InlineData("DELETE FROM items RETURNING id", false, "id")]
    [InlineData("INSERT INTO items DEFAULT VALUES", false, "")]
    [InlineData("UPDATE items SET id = 1", false, "")]
    [InlineData("DELETE FROM items", false, "")]
    public void CteOutput_InferColumnsFromQueryResults(string sql, bool relaxed, string expectedColumns)
    {
        var parser = new TextParser(new GenericLexer(), new Parser());
        Statement query = Assert.IsType<Statement>(Assert.Single(parser.ParseText(sql)), exactMatch: false);
        var body = new CommonTableExpressionBody(new Identifier("results"), null, query);

        PseudoTable result = body.ToPseudoTable(relaxed: relaxed);

        Assert.Equal("results", result.Name);
        Assert.Equal(PseudoTableType.CommonTableExpression, result.Type);
        Assert.Equal(expectedColumns, string.Join(",", result.SelectableItems));
    }

    [Fact]
    public void CteOutput_RejectsStatementsWithoutResultShape()
    {
        var parser = new TextParser(new GenericLexer(), new Parser());
        Statement query = Assert.IsType<Call>(Assert.Single(parser.ParseText("CALL audit()")));
        var body = new CommonTableExpressionBody(new Identifier("results"), null, query);

        Assert.Throws<NotSupportedException>(() => body.ToPseudoTable());
    }

    [Theory]
    [InlineData("INSERT INTO items SELECT id FROM seed", false)]
    [InlineData("INSERT INTO items SELECT id FROM seed", true)]
    [InlineData("UPDATE items SET id = seed.id FROM seed", false)]
    [InlineData("UPDATE items SET id = seed.id FROM seed", true)]
    [InlineData("DELETE FROM items USING seed WHERE items.id = seed.id", false)]
    [InlineData("DELETE FROM items USING seed WHERE items.id = seed.id", true)]
    public void DmlCtes_FilterLocalRelationsAndPreserveTargetReferences(string sql, bool withPseudoTables)
    {
        var parser = new TextParser(new GenericLexer(), new Parser());
        Statement statement = Assert.IsType<Statement>(Assert.Single(parser.ParseText($"WITH seed AS (SELECT 1 AS id) {sql}")), exactMatch: false);
        var schema = new SchemaIdentifier("app", new CatalogIdentifier("def"));
        var pseudoTables = withPseudoTables ? new PseudoTableSet(schema, []) : null;
        var context = new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.All,
            FunctionNameProvider = new FunctionNameProvider(),
            PseudoTables = pseudoTables,
            ActiveSchema = schema
        };

        ItemRef.ObjectNameRef[] references = [.. statement.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>()];

        Assert.Contains(references, reference => reference.Identifiers.Last().Name == "items");
        Assert.DoesNotContain(references, reference => reference.Identifiers.Last().Name == "seed");
        Assert.Same(pseudoTables, context.PseudoTables);
        string formatted = FormatComponent(statement);
        Assert.StartsWith("WITH seed AS (", formatted);
        Assert.Contains("\"items\"", formatted);
    }

    [Fact]
    public void ConflictOperatorClass_FormatsWithoutParameters()
    {
        var element = new PgConflictIndexElement(new SingleIdentifier(new Identifier("id")), new ObjectName([new Identifier("app"), new Identifier("int_ops")]));

        Assert.Equal("\"id\" \"app\".\"int_ops\"", FormatComponent(element));
    }

    [Fact]
    public void DerivedTable_RequiresAliasBeforeRegisteringSource()
    {
        var query = new Select(new SelectBody.SimpleSelectQuery(new SimpleSelect([new SimpleSelectItem.ExpressionWithAlias(new LiteralValue(new Value.Number("1", false)), new Identifier("id"))])));
        var factor = new TableFactor.Derived(query);
        var pseudoTables = new PseudoTableSet(null, []);
        pseudoTables.EnterSelectScope();

        Assert.Throws<SqlSyntaxException.SubqueryNotAliased>(() => factor.AddTablesToContext(pseudoTables));
        Assert.Empty(pseudoTables.GetLocalWildcardColumns());
        pseudoTables.LeaveSelectScope();
    }

    [Theory]
    [InlineData("VALUES ()", 1)]
    [InlineData("VALUES (), ()", 2)]
    public void Values_ParsesEmptyRows(string sql, int expectedRows)
    {
        var parser = new TextParser(new GenericLexer(), new Parser());

        var statement = Assert.IsType<Select>(Assert.Single(parser.ParseText(sql)));

        var body = Assert.IsType<SelectBody.ValuesQuery>(statement.Body);
        Assert.Equal(expectedRows, body.Values.Rows.Count);
        Assert.All(body.Values.Rows, row => Assert.Empty(row));
        Assert.Equal(sql, statement.ToSql());
    }

    [Fact]
    public void CteOutput_EmptyValuesHaveNoInferredColumns()
    {
        var query = new Select(new SelectBody.ValuesQuery(new Values([])));
        var body = new CommonTableExpressionBody(new Identifier("results"), null, query);

        Assert.Empty(body.ToPseudoTable().SelectableItems);
    }

    [Theory]
    [InlineData(ItemType.Unknown, true)]
    [InlineData(ItemType.Table, true)]
    [InlineData(ItemType.View, true)]
    [InlineData(ItemType.DerivedTable, true)]
    [InlineData(ItemType.Function, false)]
    [InlineData(ItemType.Procedure, false)]
    [InlineData(ItemType.BuiltInFunction, false)]
    public void CteReferences_FilterOnlyUnqualifiedRelationNames(ItemType type, bool hidesLocalRelation)
    {
        var query = new Select(new SelectBody.SimpleSelectQuery(new SimpleSelect([new SimpleSelectItem.ExpressionWithAlias(new LiteralValue(new Value.Number("1", false)), new Identifier("id"))])));
        var cte = new CommonTableExpression([new CommonTableExpressionBody(new Identifier("results"), null, query)], false);
        var local = new ItemRef.ObjectNameRef(type, new ObjectName([new Identifier("results")]));
        var qualified = new ItemRef.ObjectNameRef(type, new ObjectName([new Identifier("app"), new Identifier("results")]));
        var unrelated = new ItemRef.ObjectNameRef(type, new ObjectName([new Identifier("other")]));
        var variable = new ItemRef.SingleIdentifierRef(ItemType.Variable, new SingleIdentifier(new Identifier("results")));
        ItemRef[] references = [local, qualified, unrelated, variable];

        ItemRef[] actual = [.. cte.GetReferencedItems(new ReferencedItemsManager(), references)];

        ItemRef[] expected = hidesLocalRelation ? [qualified, unrelated, variable] : references;
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("owner_name", "\"owner_name\"")]
    [InlineData("\"MixedCase\"", "\"MixedCase\"")]
    [InlineData("`owner_name`", "\"owner_name\"")]
    [InlineData("[owner_name]", "\"owner_name\"")]
    [InlineData("'owner_name'", "'owner_name'")]
    [InlineData("CURRENT_ROLE", "CURRENT_ROLE")]
    [InlineData("CURRENT_USER", "CURRENT_USER")]
    [InlineData("SESSION_USER", "SESSION_USER")]
    [InlineData("'username'@'hostname'", "'username'@'hostname'")]
    [InlineData("[username]@`hostname`", "[username]@`hostname`")]
    [InlineData("\"CURRENT_USER\"", "\"CURRENT_USER\"")]
    public void Account_FormattingPreservesSpecialRolesAndHostAccounts(string sql, string expected)
    {
        var state = new ParserState([.. new GenericLexer().Tokenize(sql)]);
        Account account = ComponentParser.ParseAccount(state);

        Assert.Equal(sql, account.ToSql());
        Assert.Equal(expected, FormatComponent(account));
        Assert.IsType<Core.Lexing.Tokens.Eof>(state.Peek());
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
