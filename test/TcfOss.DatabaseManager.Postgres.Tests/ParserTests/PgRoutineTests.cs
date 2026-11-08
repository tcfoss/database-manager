using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgRoutineTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("INT", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("app.result[]", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("trigger", typeof(StatementFunctionReturn.PseudoType))]
    [InlineData("void", typeof(StatementFunctionReturn.PseudoType))]
    [InlineData("record", typeof(StatementFunctionReturn.PseudoType))]
    [InlineData("SETOF INT", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("SETOF app.result[]", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("SETOF record", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("SETOF void", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("SETOF \"record\"", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("TABLE (id INT, \"Label\" app.label[])", typeof(StatementFunctionReturn.Table))]
    [InlineData("\"trigger\"", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("app.record", typeof(StatementFunctionReturn.DataTypeReturn))]
    public void FunctionReturnShapes_ParseWriteAndFormat(string returnClause, Type expectedType)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement($"CREATE FUNCTION f() RETURNS {returnClause} LANGUAGE plpgsql AS $$opaque; body$$"));

        Assert.IsType(expectedType, function.ReturnType);
        Assert.Equal("opaque; body", Assert.IsType<RoutineBodyLiteral>(function.Body).Value);
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
    }

    [Theory]
    [InlineData("TABLE ()")]
    [InlineData("TABLE (value)")]
    [InlineData("TABLE (value INT NOT NULL)")]
    [InlineData("TABLE (value INT DEFAULT 1)")]
    [InlineData("TABLE (value INT PRIMARY KEY)")]
    [InlineData("TABLE (value INT,)")]
    [InlineData("SETOF")]
    [InlineData("SETOF SETOF INT")]
    [InlineData("SETOF TABLE (value INT)")]
    public void InvalidFunctionReturnShapes_AreRejected(string returnClause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE FUNCTION f() RETURNS {returnClause} LANGUAGE plpgsql AS 'opaque'"));
    }

    [Fact]
    public void RoutineParameters_ParsePostgresModesNamesAndDefaults()
    {
        const string sql = "CREATE FUNCTION f(INT, named TEXT DEFAULT 'x', INOUT count INT = 1, IN OUT total INT, VARIADIC rest INT[]) "
            + "RETURNS RECORD LANGUAGE plpgsql AS 'opaque'";
        var function = Assert.IsType<CreateFunction>(ParseStatement(sql));
        var unnamed = Assert.IsType<StatementRoutineParameter.General>(function.Parameters[0]);
        var named = Assert.IsType<StatementRoutineParameter.General>(function.Parameters[1]);
        var inout = Assert.IsType<StatementRoutineParameter.General>(function.Parameters[2]);
        var inOutAlias = Assert.IsType<StatementRoutineParameter.General>(function.Parameters[3]);
        var variadic = Assert.IsType<StatementRoutineParameter.General>(function.Parameters[4]);

        Assert.Null(unnamed.Name);
        Assert.IsType<DataTypeReference.BuiltIn>(unnamed.DataType);
        Assert.Equal("named", named.Name?.Name);
        Assert.IsType<ParameterDefault.ExpressionDefault>(named.ParameterDefault);
        Assert.Equal(RoutineParameterDirection.InOut, inout.Direction);
        Assert.IsType<ParameterDefault.ExpressionDefault>(inout.ParameterDefault);
        Assert.Equal(RoutineParameterDirection.InOut, inOutAlias.Direction);
        Assert.Equal(RoutineParameterDirection.Variadic, variadic.Direction);
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
    }

    [Theory]
    [InlineData("INT", false, typeof(DataTypeReference.BuiltIn))]
    [InlineData("INT DEFAULT 1", true, typeof(DataTypeReference.BuiltIn))]
    [InlineData("INT = 1", true, typeof(DataTypeReference.BuiltIn))]
    [InlineData("DOUBLE PRECISION", false, typeof(DataTypeReference.BuiltIn))]
    [InlineData("CHARACTER VARYING", false, typeof(DataTypeReference.BuiltIn))]
    [InlineData("TIMESTAMP WITHOUT TIME ZONE", false, typeof(DataTypeReference.BuiltIn))]
    [InlineData("TIMESTAMP WITH TIME ZONE", false, typeof(DataTypeReference.BuiltIn))]
    [InlineData("INT[] DEFAULT NULL", true, typeof(DataTypeReference.Array))]
    [InlineData("app.label DEFAULT 'x'", true, typeof(DataTypeReference.Named))]
    [InlineData("custom_type", false, typeof(DataTypeReference.Named))]
    [InlineData("custom_type DEFAULT 'x'", true, typeof(DataTypeReference.Named))]
    [InlineData("\"CustomType\"", false, typeof(DataTypeReference.Named))]
    [InlineData("\"CustomType\"[] DEFAULT NULL", true, typeof(DataTypeReference.Array))]
    [InlineData("custom_type ARRAY DEFAULT NULL", true, typeof(DataTypeReference.Array))]
    public void RoutineParameters_ParseUnnamedTypesAndDefaults(string declaration, bool hasDefault, Type dataTypeType)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement($"CREATE FUNCTION f({declaration}) RETURNS INT LANGUAGE plpgsql AS 'opaque'"));
        var parameter = Assert.IsType<StatementRoutineParameter.General>(Assert.Single(function.Parameters));

        Assert.Null(parameter.Name);
        Assert.Equal(hasDefault, parameter.ParameterDefault != null);
        Assert.IsType(dataTypeType, parameter.DataType);
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
    }

    [Theory]
    [InlineData("INT TEXT", "INT", null)]
    [InlineData("DOUBLE INT", "DOUBLE", null)]
    [InlineData("result OUT INT", "result", "OUT")]
    [InlineData("result IN OUT INT", "result", "INOUT")]
    public void RoutineParameters_ParseNamesThatCouldBeTypesAndTrailingModes(string declaration, string name, string? direction)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement($"CREATE FUNCTION f({declaration}) RETURNS INT LANGUAGE plpgsql AS 'opaque'"));
        var parameter = Assert.IsType<StatementRoutineParameter.General>(Assert.Single(function.Parameters));

        Assert.Equal(name, parameter.Name?.Name);
        Assert.Equal(direction == null ? null : RoutineParameterDirection.Parse(direction), parameter.Direction);
        Assert.IsType<DataTypeReference.BuiltIn>(parameter.DataType);
        Assert.Equal(function, ParseStatement(function.ToSql()));
    }

    [Theory]
    [InlineData("CREATE FUNCTION f(OUT result INT) LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(OUT left INT, OUT right TEXT) LANGUAGE plpgsql AS 'opaque'")]
    public void FunctionReturnMayBeOmittedWhenOutputParametersArePresent(string sql)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement(sql));

        Assert.Null(function.ReturnType);
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
    }

    [Fact]
    public void ScalarFunctionReturnConversions_PreserveTheDataTypeAndFormatting()
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement("CREATE FUNCTION f() RETURNS INT RETURN 1"));
        var scalar = Assert.IsType<StatementFunctionReturn.DataTypeReturn>(function.ReturnType);
        DataTypeReference reference = scalar.DataType;
        DataType dataType = Assert.IsType<DataTypeReference.BuiltIn>(reference).DataType;

        StatementFunctionReturn fromReference = reference;
        StatementFunctionReturn fromDataType = dataType;
        FunctionReturn resolved = dataType;

        Assert.Equal(scalar, fromReference);
        Assert.Equal(scalar, fromDataType);
        Assert.Equal(scalar, resolved.ToStatementReturn());
        Assert.Equal(scalar.ToSql(), Format(resolved));
    }

    [Theory]
    [InlineData("LANGUAGE plpgsql AS $$BEGIN RETURN 'it''s fine'; END$$", "BEGIN RETURN 'it''s fine'; END", "")]
    [InlineData("AS $body$opaque $other$ text$body$ LANGUAGE plpgsql", "opaque $other$ text", "body")]
    [InlineData("AS 'SELECT ''it''''s fine''' LANGUAGE sql", "SELECT 'it''s fine'", null)]
    [InlineData("LANGUAGE sql AS ''", "", null)]
    public void LiteralBodies_PreserveContentAndKeepOptionsOutsideBody(string tail, string value, string? tag)
    {
        string sql = "CREATE FUNCTION f() RETURNS TEXT " + tail;
        var function = Assert.IsType<CreateFunction>(ParseStatement(sql));
        var body = Assert.IsType<RoutineBodyLiteral>(function.Body);
        Assert.Equal(value, body.Value);
        Assert.Equal(tag, body.DollarQuoteTag);
        Assert.True(function.UseAsBeforeBody);
        Assert.NotNull(function.PgOptionsBeforeBody);
        Assert.NotNull(function.PgOptionsAfterBody);
        Assert.Single(function.PgOptionsBeforeBody.Concat(function.PgOptionsAfterBody));
        Assert.Null(function.MyCharacteristic);
        Assert.Empty(body.GetReferencedItems(new ReferencedItemsManager()));
        Assert.Equal(tail, SourceManager.GetText(sql, function.RoutineTailMeta));
        Assert.StartsWith(tag == null ? "'" : "$", SourceManager.GetText(sql, body.Meta)!.Trim());
        Assert.Equal(function, Assert.IsType<CreateFunction>(ParseStatement(function.ToSql())));
        Assert.Equal(function, Assert.IsType<CreateFunction>(ParseStatement(Format(function))));
    }

    [Theory]
    [InlineData("CREATE FUNCTION f() RETURNS INT RETURN 1")]
    [InlineData("CREATE FUNCTION f() RETURNS INT LANGUAGE SQL RETURN 1")]
    [InlineData("CREATE FUNCTION f() RETURNS INT RETURN 1 LANGUAGE sql")]
    [InlineData("CREATE FUNCTION f() RETURNS INT BEGIN ATOMIC RETURN 1; END")]
    [InlineData("CREATE PROCEDURE p() LANGUAGE sql BEGIN ATOMIC SELECT 1; SELECT 2; END")]
    [InlineData("CREATE PROCEDURE p() BEGIN ATOMIC END LANGUAGE sql")]
    [InlineData("CREATE FUNCTION f() RETURNS INT LANGUAGE \"sql\" RETURN 1")]
    [InlineData("CREATE PROCEDURE p() BEGIN ATOMIC ;;; SELECT $$it''s fine$$; ; END")]
    public void StructuredBodies_RoundTripThroughWritingAndFormatting(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Assert.Equal(statement, ParseStatement(Format(statement)));
    }

    [Fact]
    public void AtomicBody_WithoutClosingTrivia_RoundTripsCanonicalSql()
    {
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement("CREATE PROCEDURE p() BEGIN ATOMIC SELECT 1;END"));
        Assert.Single(Assert.IsType<BeginAtomic>(procedure.Body).Statements);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
        Assert.Equal(procedure.ToSql(), ParseStatement(Format(procedure)).ToSql());
    }

    [Theory]
    [InlineData("CREATE FUNCTION f() RETURNS INT AS RETURN 1")]
    [InlineData("CREATE FUNCTION f() RETURNS INT AS 'RETURN 1'")]
    [InlineData("CREATE FUNCTION f() RETURNS INT LANGUAGE sql SELECT 1")]
    [InlineData("CREATE FUNCTION f() RETURNS INT LANGUAGE plpgsql RETURN 1")]
    [InlineData("CREATE FUNCTION f() RETURNS INT RETURN 1 LANGUAGE plpgsql")]
    [InlineData("CREATE FUNCTION f() RETURNS INT LANGUAGE \"SQL\" RETURN 1")]
    [InlineData("CREATE FUNCTION f() RETURNS INT BEGIN RETURN 1; END")]
    [InlineData("CREATE PROCEDURE p() RETURN 1")]
    [InlineData("CREATE PROCEDURE p() SELECT 1")]
    [InlineData("CREATE PROCEDURE p() LANGUAGE plpgsql BEGIN ATOMIC SELECT 1; END")]
    [InlineData("CREATE PROCEDURE p() BEGIN ATOMIC SELECT 1; END LANGUAGE plpgsql")]
    [InlineData("CREATE PROCEDURE p() BEGIN ATOMIC SELECT 1 END")]
    [InlineData("CREATE PROCEDURE p() BEGIN ATOMIC SELECT 1;")]
    [InlineData("CREATE PROCEDURE p() BEGIN ATOMIC BEGIN ATOMIC SELECT 1; END; END")]
    [InlineData("BEGIN ATOMIC SELECT 1; END")]
    [InlineData("'not a statement'")]
    [InlineData("$$not a statement$$")]
    public void InvalidBodyShapesAndContexts_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("LANGUAGE sql LANGUAGE sql AS 'body'")]
    [InlineData("LANGUAGE sql AS 'body' LANGUAGE sql")]
    [InlineData("AS 'body' LANGUAGE sql LANGUAGE sql")]
    [InlineData("RETURN 1 LANGUAGE sql LANGUAGE sql")]
    public void DuplicateLanguages_PointAtSecondClause(string tail)
    {
        string sql = "CREATE FUNCTION f() RETURNS INT " + tail;
        Assert.Throws<ParseException.Duplicate>(() => ParseStatements(sql));
    }

    [Fact]
    public void LiteralScope_RestoresNestedStateAndExceptions()
    {
        ParserState state = GetState("'body'");
        Assert.Equal(ParseContext.None, state.Context);
        using (state.EnterContext(ParseContext.StringLiteralAsStatement))
        {
            using (state.EnterContext(ParseContext.StringLiteralAsStatement))
            {
                Assert.Equal(ParseContext.StringLiteralAsStatement, state.Context);
            }
            Assert.Equal(ParseContext.StringLiteralAsStatement, state.Context);
            Assert.IsType<RoutineBodyLiteral>(new PgParser().ParseStatement(state));
        }
        Assert.Equal(ParseContext.None, state.Context);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using (state.EnterContext(ParseContext.StringLiteralAsStatement))
            {
                throw new InvalidOperationException();
            }
        }));
        Assert.Equal(ParseContext.None, state.Context);
    }

    [Theory]
    [InlineData("FUNCTION f() RETURNS INT", true)]
    [InlineData("PROCEDURE p()", false)]
    public void EscapeStringBodies_AreDecodedAndRemainOpaque(string declaration, bool isFunction)
    {
        var statement = ParseStatement($"CREATE {declaration} LANGUAGE plpgsql AS E'BEGIN\\nRAISE NOTICE \\'it\\\\s\\';\\nEND'");
        IWriteSql body = isFunction ? Assert.IsType<CreateFunction>(statement).Body : Assert.IsType<CreateProcedure>(statement).Body;
        var literal = Assert.IsType<RoutineBodyLiteral>(body);

        Assert.Equal("BEGIN\nRAISE NOTICE 'it\\s';\nEND", literal.Value);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Assert.Equal(statement, ParseStatement(Format(statement)));
        Assert.Empty(literal.GetReferencedItems(new ReferencedItemsManager()));
    }

    [Theory]
    [InlineData("FUNCTION f() RETURNS INT", true)]
    [InlineData("PROCEDURE p()", false)]
    public void UnicodeStringBodies_AreDecodedAndRemainOpaque(string declaration, bool isFunction)
    {
        var statement = ParseStatement($"CREATE {declaration} LANGUAGE plpgsql AS U&'BEGIN!000aRAISE NOTICE ''it!!s'';!000aEND' UESCAPE '!'");
        IWriteSql body = isFunction ? Assert.IsType<CreateFunction>(statement).Body : Assert.IsType<CreateProcedure>(statement).Body;
        var literal = Assert.IsType<RoutineBodyLiteral>(body);

        Assert.Equal("BEGIN\nRAISE NOTICE 'it!s';\nEND", literal.Value);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Assert.Equal(statement, ParseStatement(Format(statement)));
        Assert.Empty(literal.GetReferencedItems(new ReferencedItemsManager()));
    }

    [Fact]
    public void AtomicScope_IsRestoredAfterFailedBodyParsing()
    {
        ParserState state = GetState("CREATE PROCEDURE p() BEGIN ATOMIC *; END; BEGIN ATOMIC END");
        var parser = new PgParser();
        Assert.ThrowsAny<ParseException>(() => parser.ParseStatement(state));
        while (state.Peek() is not Eof && !state.PeekKeywordsAllEqual(Keyword.BEGIN, Keyword.ATOMIC))
        {
            state.Next();
        }
        Assert.ThrowsAny<ParseException>(() => parser.ParseStatement(state));
    }

    [Fact]
    public void LiteralBodyEquality_IgnoresQuotingButIncludesContent()
    {
        var plain = new RoutineBodyLiteral("SELECT 'x'");
        var tagged = plain with { DollarQuoteTag = "body" };
        Assert.Equal(plain, tagged);
        Assert.Equal(plain.GetHashCode(), tagged.GetHashCode());
        Assert.NotEqual(plain, tagged with { Value = "SELECT 'y'" });
        Assert.False(plain.Equals(null));
        Assert.NotEqual<Statement>(plain, new BeginAtomic([]));
        Assert.Equal("'SELECT ''x'''", plain.ToSql());
        Assert.Equal("$body$SELECT 'x'$body$", tagged.ToSql());
    }

    [Fact]
    public void AtomicBodies_ExposeReferencesAndPreserveClosingComments()
    {
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement("CREATE PROCEDURE p() BEGIN ATOMIC SELECT id FROM items; /* closing */ END"));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Assert.IsType<InertOnly>(body.Statements[^1]);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        ItemRef[] references = [.. body.GetReferencedItems(context)];
        Assert.Contains(references, item => item.Identifiers.Any(identifier => identifier.Name == "items"));
        Assert.Empty(new BeginAtomic([]).GetReferencedItems(context));
        string formatted = Format(procedure);
        Assert.Contains("/* closing */", formatted);
        Assert.DoesNotContain("/* closing */;", formatted);
        Assert.Equal(procedure.ToSql(), ParseStatement(formatted).ToSql());
    }

    [Theory]
    [InlineData("$$it''s \\ fine$$")]
    [InlineData("$tag$'quoted' $other$$tag$")]
    public void DollarValues_RoundTripInExpressionsAndDefaults(string literal)
    {
        foreach (string sql in new[] { "SELECT " + literal, "CREATE TABLE t (note TEXT DEFAULT " + literal + ")" })
        {
            Statement statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
            Assert.Equal(statement, ParseStatement(statement.ToSql()));
            Assert.Contains(literal, statement.ToSql());
        }
        ParserState state = GetState(literal);
        Assert.Equal(Assert.IsType<DollarQuotedStringLiteral>(state.Peek()).Value, ValueParser.ParseLiteralString(state));
    }

    [Theory]
    [InlineData("literal")]
    [InlineData("statement")]
    public void LegacyRoutineWrappers_DelegateSqlFormattingAndReferences(string kind)
    {
        Statement inner = Assert.IsType<Statement>(ParseStatement("SELECT id FROM items"), exactMatch: false);
        PgRoutineBody body = kind == "literal"
            ? new PgRoutineBody.Literal("'opaque'", new Identifier("sql"))
            : new PgRoutineBody.StatementBody(inner, null);
        Assert.Equal(kind == "literal" ? "'opaque'" : inner.ToSql(), body.ToSql());
        Assert.NotEmpty(Format(body));
        ItemRef[] references = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })];
        Assert.Equal(kind == "literal", references.Length == 0);
    }

    [Theory]
    [InlineData("BEGIN ATOMIC SELECT 1; END")]
    [InlineData("AS 'SELECT 1' LANGUAGE sql")]
    [InlineData("AS $$SELECT 1$$ LANGUAGE sql")]
    [InlineData("LANGUAGE sql AS 'SELECT 1'")]
    [InlineData("LANGUAGE sql BEGIN ATOMIC SELECT 1; END")]
    [InlineData("LANGUAGE sql RETURN 1")]
    [InlineData("RETURN 1")]
    public void CoreParser_KitchenSinkDispatch_AcceptsSharedRoutineShapes(string tail)
    {
        string sql = "CREATE FUNCTION f() RETURNS INT " + tail;
        var parser = new Parser();
        var statement = Assert.IsType<CreateFunction>(Assert.Single(parser.Parse([.. new PgLexer().Tokenize(sql)])));
        Assert.Equal(statement.ToSql(), Assert.Single(parser.Parse([.. new PgLexer().Tokenize(statement.ToSql())])).ToSql());
    }

    [Theory]
    [InlineData("AS RETURN 1")]
    [InlineData("AS 1")]
    [InlineData("LANGUAGE sql SELECT 1")]
    [InlineData("LANGUAGE sql BEGIN SELECT 1; END")]
    [InlineData("LANGUAGE sql BEGIN")]
    [InlineData("LANGUAGE sql")]
    public void CoreParser_KitchenSinkDispatch_PreservesLegacyFallback(string tail)
    {
        string sql = "CREATE FUNCTION f() RETURNS INT " + tail;
        var parser = new Parser();
        if (tail is "AS RETURN 1" or "LANGUAGE sql SELECT 1" or "LANGUAGE sql BEGIN SELECT 1; END")
        {
            var statement = Assert.IsType<CreateFunction>(Assert.Single(parser.Parse([.. new PgLexer().Tokenize(sql)])));
            Assert.Null(statement.PgOptionsBeforeBody);
            Assert.Null(statement.PgOptionsAfterBody);
        }
        else
        {
            Assert.ThrowsAny<ParseException>(() => parser.Parse([.. new PgLexer().Tokenize(sql)]));
        }
    }

    [Fact]
    public void AggregateFunction_UsesSharedHeaderFormatting()
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement("CREATE FUNCTION f() RETURNS INT RETURN 1")) with { Aggregate = true };
        Assert.Contains("AGGREGATE FUNCTION", Format(function));
    }

    [Theory]
    [InlineData("IMMUTABLE RETURNS NULL ON NULL INPUT EXTERNAL SECURITY DEFINER PARALLEL SAFE COST 2.5 SET app.mode = 'x' LANGUAGE sql", "ROWS 7")]
    [InlineData("SET app.mode TO 'x' STABLE CALLED ON NULL INPUT", "LANGUAGE sql PARALLEL RESTRICTED COST 3 ROWS 9 SECURITY INVOKER")]
    [InlineData("", "LANGUAGE sql VOLATILE STRICT PARALLEL UNSAFE SET app.mode FROM CURRENT")]
    public void RoutineOptionLists_PreserveOrderSpellingAndBodyPlacement(string before, string after)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement($"CREATE FUNCTION f() RETURNS SETOF INT {before} AS $$body$$ {after}"));

        Assert.NotNull(function.PgOptionsBeforeBody);
        Assert.NotNull(function.PgOptionsAfterBody);
        Assert.Equal(before, function.PgOptionsBeforeBody.ToSqlDelimited(" "));
        Assert.Equal(after, function.PgOptionsAfterBody.ToSqlDelimited(" "));
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
    }

    [Theory]
    [InlineData("SECURITY INVOKER", "EXTERNAL SECURITY DEFINER", "SECURITY")]
    [InlineData("STRICT", "RETURNS NULL ON NULL INPUT", "STRICT")]
    [InlineData("CALLED ON NULL INPUT", "STRICT", "STRICT")]
    [InlineData("IMMUTABLE", "VOLATILE", "VOLATILITY")]
    [InlineData("PARALLEL SAFE", "PARALLEL UNSAFE", "PARALLEL")]
    [InlineData("COST 1", "COST 2", "COST")]
    [InlineData("ROWS 1", "ROWS 2", "ROWS")]
    [InlineData("SET app.mode TO 'x'", "SET APP.MODE = 'y'", "SET:app.mode")]
    [InlineData("", "LANGUAGE plpgsql", "LANGUAGE")]
    public void RoutineOptionLists_RejectDuplicateCategoriesAcrossBody(string before, string after, string category)
    {
        var error = Assert.Throws<ParseException.Duplicate>(() => ParseStatement(
            $"CREATE FUNCTION f() RETURNS SETOF INT LANGUAGE sql {before} AS $$body$$ {after}"));

        Assert.Contains(category, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("IMMUTABLE")]
    [InlineData("STRICT")]
    [InlineData("RETURNS NULL ON NULL INPUT")]
    [InlineData("CALLED ON NULL INPUT")]
    [InlineData("PARALLEL SAFE")]
    [InlineData("COST 1")]
    [InlineData("ROWS 1")]
    public void ProcedureOptions_RejectFunctionOnlyClauses(string option)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"CREATE PROCEDURE p() LANGUAGE sql {option} AS $$body$$"));
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"CREATE PROCEDURE p() LANGUAGE sql AS $$body$$ {option}"));
    }

    [Theory]
    [InlineData("COST 0")]
    [InlineData("COST -1")]
    [InlineData("ROWS 0")]
    [InlineData("ROWS -1")]
    [InlineData("PARALLEL unknown")]
    [InlineData("EXTERNAL SECURITY unknown")]
    [InlineData("SET app.mode 'x'")]
    [InlineData("SET app.mode FROM")]
    public void RoutineOptions_RejectMalformedClauses(string option)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"CREATE FUNCTION f() RETURNS SETOF INT LANGUAGE sql {option} AS $$body$$"));
    }

    [Theory]
    [InlineData("IMMUTABLE")]
    [InlineData("STABLE")]
    [InlineData("VOLATILE")]
    [InlineData("STRICT")]
    [InlineData("CALLED ON NULL INPUT")]
    [InlineData("RETURNS NULL ON NULL INPUT")]
    [InlineData("SECURITY DEFINER")]
    [InlineData("EXTERNAL SECURITY INVOKER")]
    [InlineData("PARALLEL SAFE")]
    [InlineData("COST 2")]
    [InlineData("ROWS 3")]
    [InlineData("SET app.mode TO 'x'")]
    public void CoreParser_KitchenSinkDispatch_AcceptsLeadingRoutineAttributes(string option)
    {
        var parser = new Parser();
        foreach (string tail in new[] { $"{option} AS $$body$$ LANGUAGE sql", $"LANGUAGE sql {option} AS $$body$$" })
        {
            string sql = "CREATE FUNCTION f() RETURNS SETOF INT " + tail;
            var function = Assert.IsType<CreateFunction>(Assert.Single(parser.Parse([.. new PgLexer().Tokenize(sql)])));

            Assert.Equal(ParseStatement(sql), function);
            Assert.Equal(function, Assert.Single(parser.Parse([.. new PgLexer().Tokenize(function.ToSql())])));
        }
    }

    [Theory]
    [InlineData("+2", "+2")]
    [InlineData("-2", "-2")]
    [InlineData("+1.5", "+1.5")]
    [InlineData("-1.5", "-1.5")]
    [InlineData("public", "'public'")]
    [InlineData("true", "TRUE")]
    [InlineData("false", "FALSE")]
    [InlineData("null", "NULL")]
    public void RoutineSettings_ParseSignedNumbersBareWordsAndSpecialValues(string value, string expected)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement(
            $"CREATE FUNCTION f() RETURNS INT LANGUAGE sql SET app.mode TO {value} AS $$body$$"));
        Assert.NotNull(function.PgOptionsBeforeBody);
        var setting = Assert.IsType<PgStatementRoutineOption.RoutineSetting>(function.PgOptionsBeforeBody[1]);

        Assert.Equal("SET app.mode TO " + expected, setting.Setting.ToSql());
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
    }

    [Theory]
    [InlineData("+true")]
    [InlineData("-'x'")]
    [InlineData("+")]
    [InlineData("-null")]
    public void RoutineSettings_RejectSignsWithoutNumericValues(string value)
    {
        var error = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(
            $"CREATE FUNCTION f() RETURNS INT LANGUAGE sql SET app.mode TO {value} AS $$body$$"));

        Assert.Contains("number", error.Message);
    }

    [Theory]
    [InlineData("ROWS 7 AS $$body$$ LANGUAGE sql")]
    [InlineData("AS $$body$$ LANGUAGE sql ROWS 7")]
    public void ScalarFunctions_RejectRowsOnEitherSideOfBody(string tail)
    {
        var error = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement("CREATE FUNCTION f() RETURNS INT " + tail));

        Assert.Contains("set-returning function for ROWS", error.Message);
    }

    private static string Format(IWriteSql statement)
    {
        var names = new FunctionNameProvider();
        var manager = new FormatManager
        {
            Formatting = new FormattingSettings { Quoting = IdentifierQuotationHandling.LeaveOriginal },
            FunctionNameProvider = names,
            ComponentNormalizer = new ComponentNormalizer(QuoteStyle.Ansi, names),
            Indenter = new Indenter()
        };
        using var writer = new SqlTextWriter(new StringBuilder());
        statement.FormatSql(writer, manager);
        return writer.ToString();
    }
}
