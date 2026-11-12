using System.Text;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
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
    [InlineData("trigger", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("void", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("record", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("any", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anyelement", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anyarray", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anynonarray", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anyenum", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anyrange", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anymultirange", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anycompatible", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anycompatiblearray", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anycompatiblenonarray", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anycompatiblerange", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("anycompatiblemultirange", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("cstring", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("internal", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("language_handler", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("fdw_handler", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("table_am_handler", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("index_am_handler", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("tsm_handler", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("event_trigger", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("pg_ddl_command", typeof(StatementFunctionReturn.DataTypeReturn))]
    [InlineData("SETOF INT", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("SETOF app.result[]", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("SETOF record", typeof(StatementFunctionReturn.SetOf))]
    [InlineData("SETOF anyelement", typeof(StatementFunctionReturn.SetOf))]
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
    [InlineData("trigger")]
    [InlineData("void")]
    [InlineData("record")]
    [InlineData("any")]
    [InlineData("anyelement")]
    [InlineData("anyarray")]
    [InlineData("anynonarray")]
    [InlineData("anyenum")]
    [InlineData("anyrange")]
    [InlineData("anymultirange")]
    [InlineData("anycompatible")]
    [InlineData("anycompatiblearray")]
    [InlineData("anycompatiblenonarray")]
    [InlineData("anycompatiblerange")]
    [InlineData("anycompatiblemultirange")]
    [InlineData("cstring")]
    [InlineData("internal")]
    [InlineData("language_handler")]
    [InlineData("fdw_handler")]
    [InlineData("table_am_handler")]
    [InlineData("index_am_handler")]
    [InlineData("tsm_handler")]
    [InlineData("event_trigger")]
    [InlineData("pg_ddl_command")]
    public void RoutineParameters_PreservePostgresPseudoTypes(string pseudoTypeName)
    {
        var expectedType = PgPseudoType.Parse(pseudoTypeName);
        var function = Assert.IsType<CreateFunction>(ParseStatement(
            $"CREATE FUNCTION f({pseudoTypeName}, value {pseudoTypeName}) RETURNS {pseudoTypeName} LANGUAGE plpgsql AS 'opaque'"));

        foreach (StatementRoutineParameter parameter in function.Parameters)
        {
            Assert.Equal(expectedType, Assert.IsType<DataTypeReference.PgPseudoType>(parameter.DataType).Type);
        }

        var returnType = Assert.IsType<StatementFunctionReturn.DataTypeReturn>(function.ReturnType);
        Assert.Equal(expectedType, Assert.IsType<DataTypeReference.PgPseudoType>(returnType.DataType).Type);
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
        Assert.Equal(function, Serialization.FromJson<CreateFunction>(Serialization.ToJson(function)));
    }

    [Theory]
    [InlineData("COMMENT ON FUNCTION f(anyelement) IS 'routine'")]
    [InlineData("COMMENT ON PROCEDURE p(value anyelement) IS 'routine'")]
    [InlineData("ALTER FUNCTION f(anyelement) IMMUTABLE")]
    [InlineData("DROP FUNCTION f(anyelement)")]
    public void RoutineTargets_PreservePseudoTypes(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        SqlValueList<StatementRoutineParameter>? parameters = statement switch
        {
            CommentOn { Target: PgStatementCommentTarget.Routine routine } => routine.Parameters,
            AlterRoutine alter => alter.Routine.Parameters,
            DropRoutine drop => Assert.Single(drop.Routines).Parameters,
            _ => throw new InvalidOperationException($"Unexpected routine target: {statement.GetType()}")
        };
        var parameter = Assert.Single(Assert.IsType<SqlValueList<StatementRoutineParameter>>(parameters));

        Assert.Equal(PgPseudoType.AnyElement, Assert.IsType<DataTypeReference.PgPseudoType>(parameter.DataType).Type);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Assert.Equal(statement, ParseStatement(Format(statement)));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
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
    [InlineData("app.users.id%TYPE", "app.users.id%TYPE")]
    [InlineData("anyelement%TYPE", "anyelement%TYPE")]
    [InlineData("\"App\".\"Users\".\"Id\"%type", "\"App\".\"Users\".\"Id\"%TYPE")]
    public void RoutineSignatures_PreservePgTypeOfReferences(string typeReference, string expectedReference)
    {
        string[] returnClauses =
        [
            typeReference,
            $"SETOF {typeReference}",
            $"TABLE (result {typeReference})"
        ];

        foreach (string returnClause in returnClauses)
        {
            string sql = $"CREATE FUNCTION f(value {typeReference}) RETURNS {returnClause} LANGUAGE plpgsql AS 'opaque'";
            var function = Assert.IsType<CreateFunction>(ParseStatement(sql));
            DataTypeReference.PgTypeOf parameterType =
                Assert.IsType<DataTypeReference.PgTypeOf>(Assert.Single(function.Parameters).DataType);

            Assert.Equal(expectedReference, parameterType.ToSql());
            switch (function.ReturnType)
            {
                case StatementFunctionReturn.DataTypeReturn dataTypeReturn:
                    Assert.Equal(expectedReference,
                        Assert.IsType<DataTypeReference.PgTypeOf>(dataTypeReturn.DataType).ToSql());
                    break;
                case StatementFunctionReturn.SetOf { ReturnType: StatementFunctionReturn.DataTypeReturn dataTypeReturn }:
                    Assert.Equal(expectedReference,
                        Assert.IsType<DataTypeReference.PgTypeOf>(dataTypeReturn.DataType).ToSql());
                    break;
                case StatementFunctionReturn.Table table:
                    Assert.Equal(expectedReference,
                        Assert.IsType<DataTypeReference.PgTypeOf>(Assert.Single(table.Columns).DataType).ToSql());
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected function return type: {function.ReturnType?.GetType()}");
            }

            Assert.Equal(function, ParseStatement(function.ToSql()));
            Assert.Equal(function, ParseStatement(Format(function)));
            Assert.Equal(function, Serialization.FromJson<CreateFunction>(Serialization.ToJson(function)));
        }

        var procedure = Assert.IsType<CreateProcedure>(ParseStatement(
            $"CREATE PROCEDURE p(value {typeReference}) LANGUAGE SQL BEGIN ATOMIC END"));
        DataTypeReference.PgTypeOf procedureParameter =
            Assert.IsType<DataTypeReference.PgTypeOf>(Assert.Single(procedure.Parameters).DataType);

        Assert.Equal(expectedReference, procedureParameter.ToSql());
        Assert.Equal(procedure, ParseStatement(procedure.ToSql()));
        Assert.Equal(procedure, ParseStatement(Format(procedure)));
        Assert.Equal(procedure, Serialization.FromJson<CreateProcedure>(Serialization.ToJson(procedure)));

        var unnamed = Assert.IsType<CreateFunction>(ParseStatement(
            $"CREATE FUNCTION f({typeReference}) RETURNS INT LANGUAGE plpgsql AS 'opaque'"));
        StatementRoutineParameter unnamedParameter = Assert.Single(unnamed.Parameters);
        Assert.Null(unnamedParameter.Name);
        Assert.Equal(expectedReference, Assert.IsType<DataTypeReference.PgTypeOf>(unnamedParameter.DataType).ToSql());
    }

    [Theory]
    [InlineData("CREATE TABLE sample (value app.users.id%TYPE)")]
    [InlineData("COMMENT ON FUNCTION f(app.users.id%TYPE) IS 'function'")]
    [InlineData("ALTER FUNCTION f(app.users.id%TYPE) IMMUTABLE")]
    [InlineData("DROP FUNCTION f(app.users.id%TYPE)")]
    public void PgTypeOfReferences_AreRestrictedToRoutineDefinitions(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
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
    [InlineData("BEGIN SELECT 1; END;", Label = "Plain procedural block")]
    [InlineData("DECLARE value INT;", Label = "Local declaration")]
    [InlineData("DECLARE failed CONDITION FOR SQLSTATE '45000';", Label = "Condition declaration")]
    [InlineData("DECLARE CONTINUE HANDLER FOR SQLSTATE '45000' SELECT 1;", Label = "Handler declaration")]
    [InlineData("IF TRUE THEN SELECT 1; END IF;", Label = "Conditional")]
    [InlineData("LOOP SELECT 1; END LOOP;", Label = "Loop")]
    [InlineData("WHILE TRUE LOOP SELECT 1; END LOOP;", Label = "While loop")]
    [InlineData("FOR value IN SELECT 1 LOOP SELECT 1; END LOOP;", Label = "For loop")]
    [InlineData("LEAVE outer_loop;", Label = "MySQL leave")]
    [InlineData("ITERATE outer_loop;", Label = "MySQL iterate")]
    [InlineData("OPEN cursor_name;", Label = "Procedural cursor open")]
    [InlineData("FETCH cursor_name INTO value;", Label = "Procedural cursor fetch")]
    [InlineData("value := 1;", Label = "Assignment")]
    [InlineData("RAISE NOTICE 'message';", Label = "Raise")]
    [InlineData("PERFORM app.work();", Label = "Perform")]
    [InlineData("EXECUTE 'SELECT 1';", Label = "Dynamic execution")]
    [InlineData("RETURN NEXT 1;", Label = "Return next")]
    [InlineData("RETURN QUERY SELECT 1;", Label = "Return query")]
    [InlineData("EXCEPTION WHEN OTHERS THEN SELECT 1;", Label = "Exception handler")]
    [InlineData("label: SELECT 1;", Label = "Statement label")]
    [InlineData("USE app;", Label = "MySQL use")]
    public void AtomicBodies_RejectProceduralStatements(string contents)
    {
        string[] declarations = ["FUNCTION f() RETURNS INT", "PROCEDURE p()"];
        foreach (string declaration in declarations)
        {
            Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE {declaration} LANGUAGE SQL BEGIN ATOMIC {contents} END"));
            Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE {declaration} BEGIN ATOMIC {contents} END LANGUAGE SQL"));
            Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE {declaration} BEGIN ATOMIC {contents} END"));
        }
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
        while (state.Peek() is not Eof && !state.PeekKeywordsAll(Keyword.BEGIN, Keyword.ATOMIC))
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
    [InlineData("BEGIN ATOMIC BEGIN SELECT 1; END; END")]
    [InlineData("BEGIN ATOMIC label: SELECT 1; END")]
    [InlineData("BEGIN ATOMIC DECLARE failed CONDITION FOR SQLSTATE '45000'; END")]
    [InlineData("BEGIN ATOMIC DECLARE CONTINUE HANDLER FOR SQLSTATE '45000' SELECT 1; END")]
    [InlineData("BEGIN ATOMIC OPEN cursor_name; FETCH cursor_name INTO value; LEAVE outer_loop; ITERATE outer_loop; END")]
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

    [Theory]
    [InlineData("BEGIN SELECT 1; END", Label = "Plain block")]
    [InlineData("label: SELECT 1", Label = "Statement label")]
    [InlineData("DECLARE failed CONDITION FOR SQLSTATE '45000'", Label = "Condition declaration")]
    [InlineData("DECLARE CONTINUE HANDLER FOR SQLSTATE '45000' SELECT 1", Label = "Handler declaration")]
    [InlineData("OPEN cursor_name", Label = "Cursor open")]
    [InlineData("FETCH cursor_name INTO value", Label = "Procedural fetch")]
    [InlineData("LEAVE outer_loop", Label = "Leave")]
    [InlineData("ITERATE outer_loop", Label = "Iterate")]
    [InlineData("USE app", Label = "Use")]
    public void SqlOnlyContext_NarrowsOnlyWhenEnabled(string sql)
    {
        Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);

        ParserState state = GetState(sql);
        using (state.EnterContext(ParseContext.NarrowSqlOnly))
        {
            Assert.ThrowsAny<ParseException>(() => new PgParser().ParseStatement(state));
            Assert.Equal(ParseContext.NarrowSqlOnly, state.Context);
            Assert.Null(state.StatementLabel);
        }
        Assert.Equal(ParseContext.None, state.Context);
    }

    [Theory]
    [InlineData("BEGIN ATOMIC SELECT 1; END", Label = "Atomic body")]
    [InlineData("RETURN 1", Label = "Return body")]
    [InlineData("AS $$BEGIN SELECT 1; END$$", Label = "Opaque body")]
    public void SqlOnlyContext_RoutineScopeRestoresPreviousFlags(string body)
    {
        ParserState state = GetState($"CREATE FUNCTION f() RETURNS INT LANGUAGE SQL {body}; BEGIN SELECT 2; END; label: SELECT 3");
        using (state.EnterContext(ParseContext.DomainCheck))
        {
            var statements = new PgParser().Parse(state, null);
            Assert.Equal(3, statements.Count);
            Assert.IsType<CreateFunction>(statements[0]);
            Assert.IsType<BeginEnd>(statements[1]);
            Assert.IsType<LabeledStatement>(statements[2]);
            Assert.Equal(ParseContext.DomainCheck, state.Context);
            Assert.Null(state.StatementLabel);
            Assert.IsType<Eof>(state.Peek());
        }
        Assert.Equal(ParseContext.None, state.Context);
    }

    [Theory]
    [InlineData("BEGIN ATOMIC BEGIN SELECT 1; END; END", Label = "Rejected procedural block")]
    [InlineData("BEGIN ATOMIC label: SELECT 1; END", Label = "Rejected label")]
    [InlineData("BEGIN ATOMIC SELECT 1 END", Label = "Missing terminator")]
    [InlineData("RETURN", Label = "Missing return expression")]
    public void SqlOnlyContext_RoutineScopeRestoresPreviousFlagsAfterFailure(string body)
    {
        ParserState state = GetState($"CREATE FUNCTION f() RETURNS INT LANGUAGE SQL {body}");
        using (state.EnterContext(ParseContext.DomainCheck))
        {
            Assert.ThrowsAny<ParseException>(() => new PgParser().ParseStatement(state));
            Assert.Equal(ParseContext.DomainCheck, state.Context);
            Assert.Null(state.StatementLabel);
        }
        Assert.Equal(ParseContext.None, state.Context);
    }

    [Theory]
    [InlineData(true, false, Label = "Function with leading language")]
    [InlineData(true, true, Label = "Function with trailing language")]
    [InlineData(false, false, Label = "Procedure with leading language")]
    [InlineData(false, true, Label = "Procedure with trailing language")]
    public void AtomicBodies_PreserveMixedStatementBoundariesAndDependencies(bool isFunction, bool languageAfterBody)
    {
        string[] contents =
        [
            "/* first */ WITH seed (id) AS (SELECT app.seed($1)) SELECT seed.id FROM seed",
            "INSERT INTO app.items DEFAULT VALUES RETURNING id",
            "UPDATE app.items SET id = app.next(id) WHERE id = $1 RETURNING id",
            "DELETE FROM app.items WHERE app.expired(id) RETURNING id",
            "CALL app.audit(message => $$text; END; BEGIN ATOMIC$$) /* tail */",
            isFunction ? "RETURN app.finish($1)" : "SELECT app.finish($1)"
        ];
        string declaration = isFunction ? "FUNCTION app.run(value INT) RETURNS INT" : "PROCEDURE app.run(value INT)";
        string sql = $"CREATE {declaration} {(languageAfterBody ? "" : "LANGUAGE SQL ")}BEGIN ATOMIC ;\n    "
            + string.Join(";\n    ", contents)
            + $"; ; /* closing */ END{(languageAfterBody ? " LANGUAGE SQL" : "")}; SELECT app.after_body();";
        ParserState state = GetState(sql, 137);
        var parsed = new PgParser().Parse(state, null);
        Assert.Equal(2, parsed.Count);
        Assert.IsType<Eof>(state.Peek());
        Assert.Equal(ParseContext.None, state.Context);
        Assert.Null(state.StatementLabel);
        IWriteSql routineBody = isFunction
            ? Assert.IsType<CreateFunction>(parsed[0]).Body
            : Assert.IsType<CreateProcedure>(parsed[0]).Body;
        var body = Assert.IsType<BeginAtomic>(routineBody);
        Statement[] statements = [.. body.Statements.Where(statement => statement is not InertOnly)];
        Assert.Equal(contents.Length, statements.Length);
        for (int index = 0; index < statements.Length; index++)
        {
            Statement statement = statements[index];
            Assert.Equal(contents[index], sql[statement.Meta.Start!.Value..statement.Meta.End!.Value].Trim());
            Assert.Equal(statement.Meta.End + 1, statement.Meta.EndPlusTerminatorLength);
            Assert.Equal(';', sql[statement.Meta.EndPlusTerminatorLength!.Value - 1]);
        }
        Assert.IsType<Select>(statements[0]);
        Assert.IsType<Insert>(statements[1]);
        Assert.IsType<Update>(statements[2]);
        Assert.IsType<Delete>(statements[3]);
        var call = Assert.IsType<Call>(statements[4]);
        var invocation = Assert.IsType<FunctionCall>(call.Expression);
        Assert.All(invocation.Name.Values, identifier => Assert.Equal(137, identifier.Source!.Value.SourceId));
        Assert.StartsWith("BEGIN ATOMIC", SourceManager.GetText(sql, body.Meta)!.Trim());
        Assert.EndsWith("END", SourceManager.GetText(sql, body.Meta)!.Trim());
        Assert.IsType<InertOnly>(body.Statements[^1]);
        string[] names = [.. body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()).Distinct().Order()];
        Assert.Equal(new[] { "app.audit", "app.expired", "app.finish", "app.items", "app.next", "app.seed" }, names);
        Assert.Equal("SELECT app.after_body()", parsed[1].ToSql());
        string canonical = parsed[0].ToSql();
        Assert.Equal(canonical, ParseStatement(canonical).ToSql());
        string formatted = Format(parsed[0]);
        Assert.Contains("/* first */", formatted);
        Assert.Contains("/* tail */", formatted);
        Assert.Contains("/* closing */", formatted);
        Assert.DoesNotContain("/* closing */;", formatted);
        Assert.Equal(canonical, ParseStatement(formatted).ToSql());
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
    }

    [Theory]
    [InlineData("SELECT 1 END", Label = "Missing final terminator")]
    [InlineData("SELECT 1 SELECT 2; END", Label = "Missing middle terminator")]
    [InlineData("CALL app.work() END", Label = "Unterminated call")]
    [InlineData("RETURN 1 END", Label = "Unterminated return")]
    [InlineData("SELECT 1;", Label = "Missing end")]
    [InlineData("BEGIN ATOMIC SELECT 1; END; END", Label = "Nested atomic body")]
    public void AtomicBodies_RejectIncompleteBoundariesAndRestoreScope(string contents)
    {
        ParserState state = GetState($"CREATE FUNCTION f() RETURNS INT LANGUAGE SQL BEGIN ATOMIC {contents}");
        Assert.ThrowsAny<ParseException>(() => new PgParser().ParseStatement(state));
        Assert.Equal(ParseContext.None, state.Context);
        Assert.Null(state.StatementLabel);
    }

    [Theory]
    [InlineData("sql", Label = "Quoted SQL stays opaque")]
    [InlineData("plpgsql", Label = "Quoted PL/pgSQL stays opaque")]
    public void LiteralBodies_DoNotApplyStructuredSqlRestrictions(string language)
    {
        const string contents = "DECLARE value INT; BEGIN value := app.work(); IF TRUE THEN RETURN value; END IF; END";
        var function = Assert.IsType<CreateFunction>(ParseStatement($"CREATE FUNCTION f() RETURNS INT LANGUAGE {language} AS $body${contents}$body$"));
        var body = Assert.IsType<RoutineBodyLiteral>(function.Body);
        Assert.Equal(contents, body.Value);
        Assert.Empty(body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(function, ParseStatement(Format(function)));
    }

    [Fact]
    public void AtomicBodies_RetainSqlCursorDeclarationSyntax()
    {
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement("CREATE PROCEDURE p() LANGUAGE SQL BEGIN ATOMIC DECLARE result CURSOR FOR SELECT id FROM app.items; CLOSE result; END"));
        var body = Assert.IsType<BeginAtomic>(procedure.Body);
        Assert.IsType<DeclareCursor>(body.Statements[0]);
        Assert.IsType<Close>(body.Statements[1]);
        Assert.Equal(procedure.ToSql(), ParseStatement(procedure.ToSql()).ToSql());
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
