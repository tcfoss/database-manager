using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Postgres.DefinitionBuilding;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.DefinitionBuilding;

public class PgDefinitionBuilderTests : ParserTestsBase<PgLexer, PgParser>
{
    [Fact]
    public void ProcessStatements_CollectsForwardReferencesWithoutResolvingThem()
    {
        var sourceManager = new SourceManager();
        var builder = CreateBuilder(sourceManager);
        SchemaIdentifier schema = GetSchema();
        var statements = ParseStatements("CREATE TABLE items (mood app.mood); CREATE DOMAIN app.label AS app.mood; CREATE TYPE app.mood AS ENUM ('happy'); CREATE TYPE app.details AS (label app.label)");

        builder.ProcessStatements(statements, schema, 7);

        foreach (Statement statement in statements)
        {
            var createStatement = Assert.IsType<ICreateStatement>(statement, exactMatch: false);
            ObjectHandle handle = ObjectHandle.Create(createStatement.Name, schema, NameHandling.LowercaseUnlessQuoted);
            Assert.Equal(new SourceRef(7, statement.Meta.Start!.Value, statement.Meta.End!.Value), sourceManager.GetSourceRef(handle));
        }
        PgDefinition definition = builder.ToDefinition();
        Assert.Single(definition.Tables);
        Assert.Equal(3, definition.CustomTypes.Count);
        var table = Assert.Single(definition.Tables).Value;
        Assert.Equal("mood", Assert.Single(table.Columns).Name.Name);
        Assert.Equal("mood", Assert.IsType<PgDataType.Custom>(Assert.Single(table.Columns).DataType).Name.Name);
    }

    [Theory]
    [InlineData("CREATE TYPE Mood AS ENUM (); CREATE DOMAIN mood AS TEXT")]
    [InlineData("CREATE TABLE mood (value TEXT); CREATE TYPE mood AS ENUM ()")]
    public void ProcessStatements_RejectsConflictingDeclarationNames(string sql)
    {
        var builder = CreateBuilder();

        Assert.Throws<SqlSyntaxException.DuplicateObject>(() => builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0));
    }

    [Fact]
    public void ProcessStatements_PreservesQuotedCaseAndSchemaQualification()
    {
        var sourceManager = new SourceManager();
        var builder = CreateBuilder(sourceManager);

        builder.ProcessStatements(ParseStatements("CREATE TYPE Mood AS ENUM (); CREATE TYPE \"Mood\" AS ENUM (); CREATE TYPE other.mood AS ENUM ()"), GetSchema(), 0);

        Assert.NotNull(sourceManager.GetSourceRef(new ObjectHandle("catalog", "public", "mood")));
        Assert.NotNull(sourceManager.GetSourceRef(new ObjectHandle("catalog", "public", "Mood")));
        Assert.NotNull(sourceManager.GetSourceRef(new ObjectHandle("catalog", "other", "mood")));
    }

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("CREATE VIEW visible AS SELECT 1 WITH CHECK OPTION")]
    [InlineData("CREATE VIEW visible WITH (security_barrier = TRUE) AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS SELECT 1 WITH NO DATA")]
    [InlineData("REFRESH MATERIALIZED VIEW summary WITH NO DATA")]
    [InlineData("DROP MATERIALIZED VIEW IF EXISTS app.summary, app.other CASCADE")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME TO summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME COLUMN id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET SCHEMA archive")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET (fillfactor = 70)")]
    [InlineData("ALTER MATERIALIZED VIEW summary RESET (fillfactor)")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO owner_name")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO CURRENT_ROLE")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD heap")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary CLUSTER ON summary_idx")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET WITHOUT CLUSTER")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET STATISTICS 100, SET (fillfactor = 70)")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id RESET (n_distinct)")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET STORAGE EXTENDED")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET COMPRESSION lz4")]
    [InlineData("ALTER MATERIALIZED VIEW summary NO DEPENDS ON EXTENSION ext")]
    [InlineData("ALTER MATERIALIZED VIEW ALL IN TABLESPACE fast OWNED BY CURRENT_USER SET TABLESPACE slow NOWAIT")]
    public void ProcessStatements_RejectsUnsupportedStatements(string sql)
    {
        var builder = CreateBuilder();
        var statements = ParseStatements(sql);

        var exception = Assert.Throws<DefinitionException>(() => builder.ProcessStatements(statements, GetSchema(), 17));

        Assert.Contains(statements[0].GetType().Name, exception.Message);
        Assert.Equal(new SourceRef(17, statements[0].Meta.Start!.Value, statements[0].Meta.End!.Value), exception.ObjectSourceRef);
    }

    [Fact]
    public void ProcessStatements_CollectsAcrossSourcesAndSchemas()
    {
        var sourceManager = new SourceManager();
        var builder = CreateBuilder(sourceManager);
        SchemaIdentifier schema = GetSchema();
        var otherSchema = new SchemaIdentifier("other", schema.Catalog);

        builder.ProcessStatements(ParseStatements("CREATE DOMAIN label AS other.mood"), schema, 3);
        builder.ProcessStatements(ParseStatements("CREATE TYPE mood AS ENUM ('happy')"), otherSchema, 4);

        ObjectHandle domainHandle = ObjectHandle.Create(ObjectIdentifier.FromStrings("catalog", "public", "label"), NameHandling.LowercaseUnlessQuoted);
        ObjectHandle typeHandle = ObjectHandle.Create(ObjectIdentifier.FromStrings("catalog", "other", "mood"), NameHandling.LowercaseUnlessQuoted);
        Assert.Equal(3, sourceManager.GetSourceRef(domainHandle)?.SourceId);
        Assert.Equal(4, sourceManager.GetSourceRef(typeHandle)?.SourceId);
    }

    [Fact]
    public void DefinitionModels_PreserveResolvedCustomTypeIdentities()
    {
        ObjectIdentifier tableName = ObjectIdentifier.FromStrings("catalog", "public", "items");
        ObjectIdentifier enumName = ObjectIdentifier.FromStrings("catalog", "app", "mood");
        ObjectIdentifier domainName = ObjectIdentifier.FromStrings("catalog", "app", "label");
        ObjectIdentifier compositeName = ObjectIdentifier.FromStrings("catalog", "app", "details");
        var definition = new PgDefinition();
        definition.CustomTypes.Add(enumName, new PgCustomType.Enumerated(enumName, ["sad", "happy"]));
        definition.CustomTypes.Add(domainName, new PgCustomType.Domain(domainName, new DataType.Text()));
        definition.CustomTypes.Add(compositeName, new PgCustomType.Composite(compositeName,
        [
            new PgCompositeTypeField(new Identifier("label"), new PgDataType.Custom(domainName))
        ]));
        var table = new PgTable(tableName,
        [
            new PgColumn(new ColumnIdentifier("mood", tableName), new PgDataType.Custom(enumName))
        ]);
        definition.Tables.Add(tableName, table);

        Assert.Equal(3, definition.CustomTypes.Count);
        Assert.Equal(enumName, Assert.IsType<PgDataType.Custom>(Assert.Single(table.Columns).DataType).Name);
        var domain = Assert.IsType<PgCustomType.Domain>(definition.CustomTypes[domainName]);
        Assert.IsType<DataType.Text>(domain.BaseType);
        var composite = Assert.IsType<PgCustomType.Composite>(definition.CustomTypes[compositeName]);
        Assert.Equal(domainName, Assert.IsType<PgDataType.Custom>(Assert.Single(composite.Attributes).DataType).Name);
        Assert.Equal(new[] { "sad", "happy" }, Assert.IsType<PgCustomType.Enumerated>(definition.CustomTypes[enumName]).Labels);
        Assert.Equal(new[] { "mood" }, table.ToPseudoTable().SelectableItems);
    }

    [Theory]
    [InlineData("CREATE DOMAIN loop AS loop")]
    [InlineData("CREATE DOMAIN first AS second; CREATE DOMAIN second AS first")]
    [InlineData("CREATE TYPE loop AS (child loop)")]
    [InlineData("CREATE DOMAIN first AS second; CREATE TYPE second AS (child first)")]
    [InlineData("CREATE TABLE loop (child loop)")]
    [InlineData("CREATE TABLE first (child second); CREATE TYPE second AS (child first)")]
    [InlineData("CREATE DOMAIN loop AS loop[]")]
    [InlineData("CREATE TYPE loop AS (child loop[])")]
    [InlineData("CREATE TABLE loop (child loop[])")]
    [InlineData("CREATE DOMAIN first AS second[]; CREATE TYPE second AS (child first[])")]
    public void ToDefinition_RejectsCyclicTypeDependencies(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 9);

        var exception = Assert.Throws<SqlSyntaxException.CyclicTypeDependency>(builder.ToDefinition);

        Assert.Contains("Cyclic type dependency", exception.Message);
        Assert.Contains(" -> ", exception.Message);
        Assert.Equal(9, exception.ObjectSourceRef?.SourceId);
        Assert.Equal(exception.Cycle[0], exception.Cycle[^1]);
        Assert.True(exception.Cycle.Count >= 2);
    }

    [Theory]
    [InlineData("CREATE DOMAIN label AS missing")]
    [InlineData("CREATE TYPE details AS (label missing)")]
    [InlineData("CREATE TABLE items (label missing)")]
    [InlineData("CREATE DOMAIN label AS missing[]")]
    [InlineData("CREATE TYPE details AS (label missing[])")]
    [InlineData("CREATE TABLE items (label missing[])")]
    [InlineData("CREATE FUNCTION f() RETURNS SETOF missing LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f() RETURNS TABLE (value missing[]) LANGUAGE plpgsql AS 'opaque'")]
    public void ToDefinition_RejectsMissingTypesWithSource(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 8);

        var exception = Assert.Throws<SqlSyntaxException.TypeNotFound>(builder.ToDefinition);

        Assert.Contains("missing", exception.Message);
        Assert.Equal("missing", exception.TypeName.ToSql());
        Assert.Equal("public", Assert.Single(exception.SearchedSchemas).Name);
        Assert.Equal(8, exception.ObjectSourceRef?.SourceId);
        Assert.Equal(sql.IndexOf("missing", StringComparison.Ordinal), exception.ObjectSourceRef?.Start);
    }

    [Fact]
    public void ToDefinition_ResolvesRoutineSignaturesWithoutFlatteningDomains()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE FUNCTION Echo(value label) RETURNS label RETURN value; CREATE PROCEDURE save(IN value label) BEGIN ATOMIC SELECT value; END; CREATE DOMAIN label AS TEXT"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        PgStoredFunction function = Assert.Single(definition.Functions).Value;
        PgStoredProcedure procedure = Assert.Single(definition.Procedures).Value;
        Assert.Equal("echo", function.Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.IsType<FunctionReturn.DataTypeReturn>(function.ReturnType).DataType).Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.Single(function.Parameters).DataType).Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.Single(procedure.Parameters).DataType).Name.Name);
        Assert.Equal(RoutineParameterDirection.In, Assert.IsType<RoutineParameter.Postgres>(Assert.Single(procedure.Parameters)).Direction);
        Assert.IsType<PgCustomType.Domain>(Assert.Single(definition.CustomTypes).Value);
    }

    [Fact]
    public void ToDefinition_InferFunctionReturnsFromOutputParameters()
    {
        const string sql = "CREATE DOMAIN label AS TEXT; "
            + "CREATE FUNCTION one_result(IN value INT, OUT result label) LANGUAGE plpgsql AS 'opaque'; "
            + "CREATE FUNCTION multiple_results(OUT number INT, OUT label TEXT) LANGUAGE plpgsql AS 'opaque'";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        var functions = builder.ToDefinition().Functions;
        PgStoredFunction one = functions[new ObjectHandle("catalog", "public", "one_result")];
        PgStoredFunction multiple = functions[new ObjectHandle("catalog", "public", "multiple_results")];

        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.IsType<FunctionReturn.DataTypeReturn>(one.ReturnType).DataType).Name.Name);
        Assert.Equal(PgPseudoType.Record, Assert.IsType<FunctionReturn.PseudoType>(multiple.ReturnType).Type);
        Assert.Equal("result", Assert.IsType<RoutineParameter.Postgres>(one.Parameters[1]).Name!.Name);
        Assert.Null(Assert.IsType<RoutineParameter.Postgres>(multiple.Parameters[0]).DefaultExpression);
    }

    [Theory]
    [InlineData("OUT result INT", "INT", false)]
    [InlineData("OUT result INT", "SETOF INT", true)]
    [InlineData("INOUT result INT", "SETOF INT", true)]
    [InlineData("OUT first INT, OUT second TEXT", "RECORD", false)]
    [InlineData("OUT first INT, OUT second TEXT", "SETOF RECORD", true)]
    public void ToDefinition_OutputParametersMatchScalarAndSetReturns(string parameters, string returnType, bool isSet)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE FUNCTION f({parameters}) RETURNS {returnType} LANGUAGE plpgsql AS 'opaque'"), GetSchema(), 0);

        PgStoredFunction function = Assert.Single(builder.ToDefinition().Functions).Value;

        Assert.Equal(isSet, function.ReturnType is FunctionReturn.SetOf);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(function.ToCreateStatement(true).ToSql()), GetSchema(), 1);
        PgStoredFunction rebuiltFunction = Assert.Single(rebuilt.ToDefinition().Functions).Value;
        Assert.Equal(function.Parameters, rebuiltFunction.Parameters);
        Assert.Equal(function.ReturnType, rebuiltFunction.ReturnType);
    }

    [Theory]
    [InlineData("CREATE FUNCTION f(VARIADIC rest INT[]) RETURNS INT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(VARIADIC rest INT[], OUT result INT) LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE PROCEDURE p(VARIADIC rest INT[]) LANGUAGE plpgsql AS 'opaque'")]
    public void ToDefinition_AcceptsVariadicArrayParameters(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();
        RoutineParameter parameter = definition.Functions.Count > 0
            ? definition.Functions.Values.Single().Parameters[0]
            : definition.Procedures.Values.Single().Parameters[0];

        Assert.Equal(RoutineParameterDirection.Variadic, Assert.IsType<RoutineParameter.Postgres>(parameter).Direction);
        Assert.IsType<PgDataType.Array>(parameter.DataType);
    }

    [Theory]
    [InlineData("CREATE FUNCTION f(OUT result INT) RETURNS TEXT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(OUT result INT) RETURNS RECORD LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(OUT result INT) RETURNS SETOF TEXT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(OUT first INT, OUT second TEXT) RETURNS SETOF INT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(OUT first INT, OUT second TEXT) RETURNS VOID LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(OUT result INT) RETURNS TABLE (result INT) LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f() LANGUAGE plpgsql AS 'opaque'")]
    public void ToDefinition_RejectsMissingOrConflictingOutputReturnTypes(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.ThrowsAny<SqlSyntaxException>(builder.ToDefinition);
    }

    [Theory]
    [InlineData("CREATE FUNCTION f(OUT result INT DEFAULT 1) RETURNS INT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(IN first INT DEFAULT 1, IN second INT) RETURNS INT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(VARIADIC first INT[], VARIADIC second INT[]) RETURNS INT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(VARIADIC first INT[], IN second INT) RETURNS INT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION f(VARIADIC rest INT) RETURNS INT LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE PROCEDURE p(VARIADIC rest TEXT) LANGUAGE plpgsql AS 'opaque'")]
    public void ToDefinition_RejectsInvalidOutputAndDefaultParameterCombinations(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.ThrowsAny<SqlSyntaxException>(builder.ToDefinition);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ToDefinition_ResolvesDirectedParameterDefaults(bool useExplicitDirection, bool useExpressionDefault)
    {
        var parsed = Assert.IsType<CreateFunction>(Assert.Single(ParseStatements("CREATE FUNCTION f(value INT) RETURNS INT RETURN 1")));
        StatementRoutineParameter original = Assert.Single(parsed.Parameters);
        var expression = new LiteralValue(new Value.Number("1", false));
        ParameterDefault parameterDefault = useExpressionDefault
            ? new ParameterDefault.ExpressionDefault(expression, true)
            : new ParameterDefault.ValueDefault(expression.Value, true);
        CreateFunction statement = parsed with
        {
            Parameters = [new StatementRoutineParameter.Directed(original.Name!, original.DataType,
                useExplicitDirection ? RoutineParameterDirection.In : null, parameterDefault)]
        };
        var builder = CreateBuilder();
        builder.ProcessStatements([statement], GetSchema(), 0);

        var parameter = Assert.IsType<RoutineParameter.Postgres>(Assert.Single(Assert.Single(builder.ToDefinition().Functions).Value.Parameters));

        Assert.Equal(RoutineParameterDirection.In, parameter.Direction);
        Assert.Equal("value", parameter.Name!.Name);
        Assert.Equal(expression, parameter.DefaultExpression);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToDefinition_RejectsSqlServerParameterShapes(bool useGeneralParameter)
    {
        var parsed = Assert.IsType<CreateFunction>(Assert.Single(ParseStatements("CREATE FUNCTION f(value INT) RETURNS INT RETURN 1")));
        StatementRoutineParameter original = Assert.Single(parsed.Parameters);
        StatementRoutineParameter parameter = useGeneralParameter
            ? new StatementRoutineParameter.General(original.Name, original.DataType, null, null, new MsRoutineParameterOptions(ReadOnly: true))
            : new StatementRoutineParameter.Undirected(original.Name!, original.DataType);
        var builder = CreateBuilder();
        builder.ProcessStatements([parsed with { Parameters = [parameter] }], GetSchema(), 0);

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialect>(builder.ToDefinition);

        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Contains("SQL Server routine parameter options", exception.Contents);
    }

    [Theory]
    [InlineData("value INT, VALUE INT")]
    [InlineData("\"value\" INT, value INT")]
    public void ToDefinition_RejectsDuplicateNormalizedParameterNames(string parameters)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE FUNCTION f({parameters}) RETURNS INT RETURN 1"), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(builder.ToDefinition);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Routines_UseCoreContractsAndRoundTripResolvedSignatures(bool includeSchema)
    {
        const string types = "CREATE DOMAIN app.label AS TEXT; CREATE TYPE app.mood AS ENUM ('happy')";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"{types}; CREATE FUNCTION Echo(value app.label) RETURNS app.mood RETURN 'happy'; CREATE PROCEDURE save(IN value app.label) BEGIN ATOMIC SELECT value; END"), GetSchema(), 0);
        PgDefinition original = builder.ToDefinition();
        StoredFunction<RoutineParameter.Postgres> function = Assert.Single(original.Functions).Value;
        StoredProcedure<RoutineParameter.Postgres> procedure = Assert.Single(original.Procedures).Value;

        CreateFunction functionStatement = function.ToCreateStatement(includeSchema);
        CreateProcedure procedureStatement = procedure.ToCreateStatement(includeSchema);

        Assert.Equal(includeSchema ? 2 : 1, functionStatement.Name.Values.Count);
        Assert.Equal(includeSchema ? 2 : 1, procedureStatement.Name.Values.Count);
        Assert.Equal(ObjectType.Function, function.ObjectType);
        Assert.Equal(ObjectType.Procedure, procedure.ObjectType);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements($"{types}; {functionStatement.ToSql()}; {procedureStatement.ToSql()}"), GetSchema(), 1);
        PgDefinition result = rebuilt.ToDefinition();
        StoredFunction<RoutineParameter.Postgres> rebuiltFunction = Assert.Single(result.Functions).Value;
        StoredProcedure<RoutineParameter.Postgres> rebuiltProcedure = Assert.Single(result.Procedures).Value;
        Assert.Equal(function.Name, rebuiltFunction.Name);
        Assert.Equal(function.Parameters, rebuiltFunction.Parameters);
        Assert.Equal(function.ReturnType, rebuiltFunction.ReturnType);
        Assert.Equal(function.Body.ToSql(), rebuiltFunction.Body.ToSql());
        Assert.Equal(procedure.Name, rebuiltProcedure.Name);
        Assert.Equal(procedure.Parameters, rebuiltProcedure.Parameters);
        Assert.Equal(procedure.Body.ToSql(), rebuiltProcedure.Body.ToSql());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TablesAndCustomTypes_RoundTripThroughCoreStatementContracts(bool includeSchema)
    {
        const string sql = "CREATE TYPE \"Mood\" AS ENUM ('happy', 'it''s fine'); "
            + "CREATE DOMAIN label AS TEXT COLLATE \"C\" DEFAULT 'x' CONSTRAINT required NOT NULL CONSTRAINT valid CHECK (VALUE <> ''); "
            + "CREATE TYPE details AS (name label COLLATE \"C\", mood \"Mood\"); "
            + "CREATE TABLE items (payload details, id INT CONSTRAINT required_id NOT NULL CONSTRAINT default_id DEFAULT 1, note label DEFAULT NULL, calculated INT DEFAULT (1 + 2), nullable TEXT NULL)";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);
        PgDefinition original = builder.ToDefinition();
        PgTable table = Assert.Single(original.Tables).Value;
        CreateTable tableStatement = table.ToCreateStatement(includeSchema, null!);
        Assert.Equal(ObjectType.Table, table.ObjectType);
        Assert.Equal(includeSchema ? 2 : 1, tableStatement.Name.Values.Count);
        var statements = new List<Statement> { tableStatement };
        foreach (PgCustomType type in original.CustomTypes.Values)
        {
            Statement statement = type.ToCreateStatement(includeSchema, null!);
            Assert.Equal(ObjectType.Type, type.ObjectType);
            Assert.Equal(includeSchema ? 2 : 1, Assert.IsType<ICreateStatement>(statement, exactMatch: false).Name.Values.Count);
            statements.Add(statement);
        }
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(string.Join("; ", statements.Select(statement => statement.ToSql()))), GetSchema(), 1);

        PgDefinition result = rebuilt.ToDefinition();

        Assert.Equal(original.Tables, result.Tables);
        Assert.Equal(original.CustomTypes, result.CustomTypes);
        Assert.Equal(table.ToCreateStatement(includeSchema).ToSql(), Assert.Single(result.Tables).Value.ToCreateStatement(includeSchema).ToSql());
    }

    [Theory]
    [InlineData("CREATE OR REPLACE FUNCTION Echo() RETURNS INT RETURN 1; CREATE OR REPLACE PROCEDURE save() BEGIN ATOMIC SELECT 1; END")]
    [InlineData("CREATE FUNCTION IF NOT EXISTS Echo() RETURNS INT RETURN 1; CREATE PROCEDURE IF NOT EXISTS save() BEGIN ATOMIC SELECT 1; END")]
    public void Routines_DiscardCreationSyntaxAndPreserveCoreRawBodyContract(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);
        PgDefinition definition = builder.ToDefinition();
        PgStoredFunction function = Assert.Single(definition.Functions).Value;
        PgStoredProcedure procedure = Assert.Single(definition.Procedures).Value;

        CreateFunction functionStatement = function.ToCreateStatement(true);
        CreateProcedure procedureStatement = procedure.ToCreateStatement(true);

        Assert.Null(functionStatement.CreateOrLabel);
        Assert.Null(procedureStatement.CreateOrLabel);
        Assert.False(functionStatement.IfNotExists);
        Assert.False(procedureStatement.IfNotExists);
        Assert.False(functionStatement.UseAsBeforeBody);
        Assert.False(procedureStatement.UseAsBeforeBody);
        var plainBuilder = CreateBuilder();
        plainBuilder.ProcessStatements(ParseStatements("CREATE FUNCTION Echo() RETURNS INT RETURN 1; CREATE PROCEDURE save() BEGIN ATOMIC SELECT 1; END"), GetSchema(), 0);
        PgDefinition plain = plainBuilder.ToDefinition();
        Assert.Equal(Assert.Single(plain.Functions).Value.ToCreateStatement(true).ToSql(), functionStatement.ToSql());
        Assert.Equal(Assert.Single(plain.Procedures).Value.ToCreateStatement(true).ToSql(), procedureStatement.ToSql());
        var functionWithRawBody = function with { RawBodyText = "RETURN 1 /* raw body */" };
        var procedureWithRawBody = procedure with { RawBodyText = "SELECT 1 /* raw body */" };
        Assert.Equal(function, functionWithRawBody);
        Assert.Equal(function.GetHashCode(), functionWithRawBody.GetHashCode());
        Assert.Equal(procedure, procedureWithRawBody);
        Assert.Equal(procedure.GetHashCode(), procedureWithRawBody.GetHashCode());
        Assert.Equal(functionWithRawBody.RawBodyText, functionWithRawBody.ToCreateStatement(true).Meta.RawText);
        Assert.Equal(procedureWithRawBody.RawBodyText, procedureWithRawBody.ToCreateStatement(true).Meta.RawText);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements($"{functionStatement.ToSql()}; {procedureStatement.ToSql()}"), GetSchema(), 1);
        PgDefinition result = rebuilt.ToDefinition();
        Assert.Equal(functionStatement.ToSql(), Assert.Single(result.Functions).Value.ToCreateStatement(true).ToSql());
        Assert.Equal(procedureStatement.ToSql(), Assert.Single(result.Procedures).Value.ToCreateStatement(true).ToSql());
    }

    [Theory]
    [InlineData("CREATE FUNCTION echo(value missing) RETURNS TEXT RETURN value")]
    [InlineData("CREATE FUNCTION echo(value TEXT) RETURNS missing RETURN value")]
    [InlineData("CREATE PROCEDURE save(value missing) BEGIN ATOMIC SELECT value; END")]
    [InlineData("CREATE FUNCTION echo(value missing[]) RETURNS TEXT RETURN value")]
    [InlineData("CREATE FUNCTION echo(value TEXT) RETURNS missing[] RETURN value")]
    [InlineData("CREATE PROCEDURE save(value missing[]) BEGIN ATOMIC SELECT value; END")]
    public void ToDefinition_RejectsMissingRoutineSignatureTypes(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.TypeNotFound>(builder.ToDefinition);
    }

    [Theory]
    [InlineData("RETURN 1", "sql")]
    [InlineData("LANGUAGE SQL RETURN 1", "sql")]
    [InlineData("LANGUAGE PLPGSQL AS $$opaque body$$", "plpgsql")]
    [InlineData("AS 'opaque body' LANGUAGE plpgsql", "plpgsql")]
    public void RoutineOptions_AreNormalizedAndPreservedInGeneratedStatements(string tail, string language)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE FUNCTION f() RETURNS INT " + tail), GetSchema(), 0);
        PgStoredFunction function = Assert.Single(builder.ToDefinition().Functions).Value;
        Assert.NotNull(function.Options);
        Assert.Equal(language, function.Options.Language.Name);
        CreateFunction generated = function.ToCreateStatement(true);
        Assert.Equal(function.Options.ToStatementOptions(), generated.PgOptionsBeforeBody);
        Assert.NotNull(generated.PgOptionsAfterBody);
        Assert.Empty(generated.PgOptionsAfterBody);
        Assert.Equal(function.Body is RoutineBodyLiteral, generated.UseAsBeforeBody);

        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(generated.ToSql()), GetSchema(), 0);
        PgStoredFunction roundTrip = Assert.Single(rebuilt.ToDefinition().Functions).Value;
        Assert.Equal(function, roundTrip);
        Assert.Equal(function.GetHashCode(), roundTrip.GetHashCode());
        Assert.NotEqual(function, function with { Options = function.Options with { Language = new Identifier("other") } });
    }

    [Theory]
    [InlineData("AS $$opaque body$$ LANGUAGE plpgsql")]
    [InlineData("LANGUAGE SQL BEGIN ATOMIC SELECT 1; END")]
    public void ProcedureOptions_AndRawTail_ArePreservedWithoutDuplicateHeaders(string tail)
    {
        string sql = "CREATE PROCEDURE p() " + tail;
        var parsed = Assert.IsType<CreateProcedure>(ParseStatement(sql));
        var builder = CreateBuilder();
        builder.ProcessStatements([parsed], GetSchema(), 0);
        PgStoredProcedure procedure = Assert.Single(builder.ToDefinition().Procedures).Value;
        Assert.NotNull(procedure.Options);
        CreateProcedure generated = procedure.ToCreateStatement(true);
        Assert.Equal(procedure.Options.ToStatementOptions(), generated.PgOptionsBeforeBody);
        Assert.NotNull(generated.PgOptionsAfterBody);
        Assert.Empty(generated.PgOptionsAfterBody);
        Assert.Equal(procedure.Body is RoutineBodyLiteral, generated.UseAsBeforeBody);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(generated.ToSql()), GetSchema(), 0);
        Assert.Equal(procedure, Assert.Single(rebuilt.ToDefinition().Procedures).Value);
        Assert.NotEqual(procedure, procedure with { Options = procedure.Options with { Language = new Identifier("other") } });

        string rawTail = SourceManager.GetText(sql, parsed.RoutineTailMeta)!;
        var withRaw = procedure with { RawBodyText = rawTail };
        string rawSql = withRaw.ToCreateStatement(true).ToSql(new WriteOptions { PreferRawText = true });
        Assert.EndsWith(tail, rawSql);
        Assert.Equal(1, rawSql.Split("LANGUAGE", StringSplitOptions.None).Length - 1);
        Assert.Equal(generated.ToSql(), withRaw.ToCreateStatement(true).ToSql(new WriteOptions { PreferRawText = false }));
    }

    [Theory]
    [InlineData("LANGUAGE sql AS 'SELECT 1'")]
    [InlineData("AS $$SELECT 1$$ LANGUAGE sql")]
    public void FunctionRawTail_ReplacesOptionsAndBodyTogether(string tail)
    {
        string sql = "CREATE FUNCTION f() RETURNS INT " + tail;
        var parsed = Assert.IsType<CreateFunction>(ParseStatement(sql));
        string rawTail = SourceManager.GetText(sql, parsed.RoutineTailMeta)!;
        parsed.Meta.RawText = rawTail;
        string rawSql = parsed.ToSql(new WriteOptions { PreferRawText = true });
        Assert.EndsWith(tail, rawSql);
        Assert.Equal(1, rawSql.Split("LANGUAGE", StringSplitOptions.None).Length - 1);
        Assert.Equal(parsed, ParseStatement(rawSql));
    }

    [Fact]
    public void ManuallyConstructedRoutine_WithoutOptions_RetainsBodyFallback()
    {
        var parsed = Assert.IsType<CreateProcedure>(ParseStatement("CREATE PROCEDURE p() BEGIN ATOMIC SELECT 1; END"));
        var statement = parsed with
        {
            Body = Assert.IsType<Statement>(ParseStatement("SELECT 1"), exactMatch: false),
            RoutineTailMeta = new MetaData()
        };
        var builder = CreateBuilder();
        builder.ProcessStatements([statement], GetSchema(), 0);
        PgStoredProcedure procedure = Assert.Single(builder.ToDefinition().Procedures).Value;
        Assert.Null(procedure.Options);
        Assert.Same(statement.Body, procedure.Body);
        CreateProcedure generatedProcedure = procedure.ToCreateStatement(true);
        Assert.Null(generatedProcedure.PgOptionsBeforeBody);
        Assert.Empty(generatedProcedure.PgOptionsAfterBody!);
        Assert.Same(statement.Body, generatedProcedure.Body);
        Assert.Contains("SELECT 1", generatedProcedure.ToSql());

        var function = Assert.IsType<CreateFunction>(ParseStatement("CREATE FUNCTION f() RETURNS INT RETURN 1")) with
        {
            Body = statement.Body,
            RoutineTailMeta = new MetaData()
        };
        var functionBuilder = CreateBuilder();
        functionBuilder.ProcessStatements([function], GetSchema(), 0);
        PgStoredFunction definition = Assert.Single(functionBuilder.ToDefinition().Functions).Value;
        Assert.Null(definition.Options);
        Assert.Same(function.Body, definition.Body);
        CreateFunction generatedFunction = definition.ToCreateStatement(true);
        Assert.Null(generatedFunction.PgOptionsBeforeBody);
        Assert.Empty(generatedFunction.PgOptionsAfterBody!);
        Assert.Same(function.Body, generatedFunction.Body);
        Assert.Contains("SELECT 1", generatedFunction.ToSql());
    }

    [Theory]
    [InlineData("INT", "RETURN 1", "sql", 100f, 0f)]
    [InlineData("INT", "LANGUAGE SQL RETURN 1", "sql", 100f, 0f)]
    [InlineData("INT", "LANGUAGE c AS 'symbol'", "c", 1f, 0f)]
    [InlineData("INT", "LANGUAGE internal AS 'symbol'", "internal", 1f, 0f)]
    [InlineData("INT", "LANGUAGE plpgsql AS $$body$$", "plpgsql", 100f, 0f)]
    [InlineData("SETOF INT", "LANGUAGE sql AS $$body$$", "sql", 100f, 1000f)]
    [InlineData("TABLE(value INT)", "LANGUAGE sql AS $$body$$", "sql", 100f, 1000f)]
    public void FunctionOptions_ResolveEffectiveDefaults(string returnType, string tail, string language, float cost, float rows)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE FUNCTION f() RETURNS {returnType} {tail}"), GetSchema(), 0);
        PgStoredFunction function = Assert.Single(builder.ToDefinition().Functions).Value;

        Assert.NotNull(function.Options);
        Assert.Equal(language, function.Options.Language.Name);
        Assert.Equal(PgRoutineVolatility.Volatile, function.Options.Volatility);
        Assert.False(function.Options.Strict);
        Assert.Equal(SecurityContext.Invoker, function.Options.Security);
        Assert.Equal(PgRoutineParallel.Unsafe, function.Options.Parallel);
        Assert.Equal(cost, function.Options.Cost);
        Assert.Equal(rows, function.Options.Rows);
        Assert.Empty(function.Options.Settings);
        Assert.Equal(rows > 0, function.Options.ToStatementOptions().OfType<PgStatementRoutineOption.Rows>().Any());

        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(function.ToCreateStatement(true).ToSql()), GetSchema(), 1);
        Assert.Equal(function, Assert.Single(rebuilt.ToDefinition().Functions).Value);
    }

    [Theory]
    [InlineData("LANGUAGE sql IMMUTABLE STRICT SECURITY DEFINER PARALLEL SAFE COST 2.5 ROWS 7 SET app.mode TO 'x'", "")]
    [InlineData("IMMUTABLE RETURNS NULL ON NULL INPUT EXTERNAL SECURITY DEFINER", "LANGUAGE sql PARALLEL SAFE COST 2.500 ROWS 7.0 SET app.mode = 'x'")]
    public void FunctionOptions_DiscardEquivalentSyntaxAndNormalizeEstimates(string before, string after)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE FUNCTION f() RETURNS SETOF INT {before} AS $$body$$ {after}"), GetSchema(), 0);
        PgStoredFunction function = Assert.Single(builder.ToDefinition().Functions).Value;
        Assert.NotNull(function.Options);
        Assert.Equal(PgRoutineVolatility.Immutable, function.Options.Volatility);
        Assert.True(function.Options.Strict);
        Assert.Equal(SecurityContext.Definer, function.Options.Security);
        Assert.Equal(PgRoutineParallel.Safe, function.Options.Parallel);
        Assert.Equal(2.5f, function.Options.Cost);
        Assert.Equal(7f, function.Options.Rows);
        Assert.IsType<PgRoutineSetting.ToValues>(Assert.Single(function.Options.Settings));

        var plainBuilder = CreateBuilder();
        plainBuilder.ProcessStatements(ParseStatements("CREATE FUNCTION f() RETURNS SETOF INT LANGUAGE sql IMMUTABLE STRICT SECURITY DEFINER PARALLEL SAFE COST 2.5 ROWS 7 SET app.mode TO 'x' AS $$body$$"), GetSchema(), 0);
        Assert.Equal(Assert.Single(plainBuilder.ToDefinition().Functions).Value, function);
        string sql = function.ToCreateStatement(true).ToSql();
        Assert.DoesNotContain("EXTERNAL", sql);
        Assert.Contains("SET app.mode TO 'x'", sql);
        Assert.DoesNotContain("RETURNS NULL ON NULL INPUT", sql);
    }

    [Theory]
    [InlineData("BEGIN ATOMIC SELECT 1; END")]
    [InlineData("LANGUAGE sql SECURITY INVOKER BEGIN ATOMIC SELECT 1; END")]
    public void ProcedureOptions_HaveOnlyEffectiveCommonAttributes(string tail)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE PROCEDURE p() " + tail), GetSchema(), 0);
        PgStoredProcedure procedure = Assert.Single(builder.ToDefinition().Procedures).Value;

        Assert.NotNull(procedure.Options);
        Assert.Equal(SecurityContext.Invoker, procedure.Options.Security);
        Assert.Empty(procedure.Options.Settings);
        Assert.Collection(procedure.Options.ToStatementOptions(),
            option => Assert.IsType<PgStatementRoutineOption.Language>(option),
            option => Assert.IsType<PgStatementRoutineOption.Security>(option));
    }

    [Fact]
    public void ProcedureSettings_NormalizeDelimiterAndRoundTripThroughEmission()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(
            "CREATE PROCEDURE p() LANGUAGE sql SET app.mode = 'x' AS $$body$$ SET app.limit TO 2"), GetSchema(), 0);
        PgStoredProcedure procedure = Assert.Single(builder.ToDefinition().Procedures).Value;
        Assert.NotNull(procedure.Options);
        Assert.Collection(procedure.Options.Settings,
            setting => Assert.Equal("SET app.mode TO 'x'", setting.ToSql()),
            setting => Assert.Equal("SET app.limit TO 2", setting.ToSql()));

        string sql = procedure.ToCreateStatement(true).ToSql();

        Assert.Contains("SET app.mode TO 'x' SET app.limit TO 2", sql);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(sql), GetSchema(), 1);
        Assert.Equal(procedure, Assert.Single(rebuilt.ToDefinition().Procedures).Value);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void RoutineSettings_RejectFromCurrentDuringDefinitionConstruction(bool function, bool beforeBody)
    {
        string header = function ? "CREATE FUNCTION f() RETURNS INT" : "CREATE PROCEDURE p()";
        string tail = beforeBody ? "SET app.mode FROM CURRENT AS $$body$$" : "AS $$body$$ SET app.mode FROM CURRENT";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"{header} LANGUAGE sql {tail}"), GetSchema(), 7);

        var error = Assert.Throws<DefinitionException.RoutineSettingFromCurrent>(builder.ToDefinition);
        Assert.NotNull(error.ObjectSourceRef);
        Assert.Equal(7, error.ObjectSourceRef.Value.SourceId);
        Assert.Contains("app.mode", error.Message);
        Assert.Contains(function ? "f" : "p", error.Message);
    }

    [Fact]
    public void NullHandlingClause_DoesNotBecomeAnExplicitReturnType()
    {
        var statement = Assert.IsType<CreateFunction>(ParseStatement(
            "CREATE FUNCTION f(OUT value INT) RETURNS NULL ON NULL INPUT LANGUAGE sql AS $$body$$"));
        Assert.Null(statement.ReturnType);
        var builder = CreateBuilder();
        builder.ProcessStatements([statement], GetSchema(), 0);
        PgStoredFunction function = Assert.Single(builder.ToDefinition().Functions).Value;

        var returnType = Assert.IsType<FunctionReturn.DataTypeReturn>(function.ReturnType);
        Assert.IsType<DataType.Int>(returnType.DataType);
        Assert.NotNull(function.Options);
        Assert.True(function.Options.Strict);
        Assert.Equal(0f, function.Options.Rows);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("NaN", false)]
    [InlineData("Infinity", false)]
    [InlineData("not-a-number", false)]
    [InlineData("1e40", false)]
    [InlineData("1e-50", false)]
    [InlineData("0", true)]
    [InlineData("-1", true)]
    [InlineData("NaN", true)]
    [InlineData("1e40", true)]
    public void RoutineEstimates_RejectInvalidManualNumericValues(string value, bool rows)
    {
        var statement = Assert.IsType<CreateFunction>(ParseStatement("CREATE FUNCTION f() RETURNS SETOF INT RETURN 1"));
        var number = new Value.Number(value, false);
        PgStatementRoutineOption option = rows
            ? new PgStatementRoutineOption.Rows(number)
            : new PgStatementRoutineOption.Cost(number);
        statement = statement with { PgOptionsBeforeBody = [option] };
        var builder = CreateBuilder();
        builder.ProcessStatements([statement], GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);
    }

    [Fact]
    public void RoutineOptions_RejectInapplicableManualAstOptions()
    {
        var scalar = Assert.IsType<CreateFunction>(ParseStatement("CREATE FUNCTION f() RETURNS INT RETURN 1")) with
        {
            PgOptionsBeforeBody = [new PgStatementRoutineOption.Rows(new Value.Number("1", false))]
        };
        var scalarBuilder = CreateBuilder();
        scalarBuilder.ProcessStatements([scalar], GetSchema(), 0);
        Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(scalarBuilder.ToDefinition);

        PgStatementRoutineOption[] functionOnly =
        [
            new PgStatementRoutineOption.Volatility(PgRoutineVolatility.Stable),
            new PgStatementRoutineOption.NullHandling(PgRoutineNullHandling.Strict),
            new PgStatementRoutineOption.Parallel(PgRoutineParallel.Safe),
            new PgStatementRoutineOption.Cost(new Value.Number("1", false)),
            new PgStatementRoutineOption.Rows(new Value.Number("1", false))
        ];
        var procedure = Assert.IsType<CreateProcedure>(ParseStatement("CREATE PROCEDURE p() BEGIN ATOMIC SELECT 1; END"));
        foreach (PgStatementRoutineOption option in functionOnly)
        {
            var builder = CreateBuilder();
            builder.ProcessStatements([procedure with { PgOptionsAfterBody = [option] }], GetSchema(), 0);
            Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);
        }
    }

    [Fact]
    public void ToDefinition_PreservesSharedAcyclicDependenciesAndTableRowTypes()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (payload details); CREATE TYPE details AS (first label, second label); CREATE DOMAIN label AS mood; CREATE TYPE mood AS ENUM ('happy')"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        Assert.Single(definition.Tables);
        Assert.Equal(3, definition.CustomTypes.Count);
        var table = Assert.Single(definition.Tables).Value;
        Assert.Equal("details", Assert.IsType<PgDataType.Custom>(Assert.Single(table.Columns).DataType).Name.Name);
    }

    [Fact]
    public void ToDefinition_ResolvesTableRowTypes()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TYPE wrapper AS (row_value items); CREATE TABLE items (id INT)"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var composite = Assert.IsType<PgCustomType.Composite>(Assert.Single(definition.CustomTypes).Value);
        Assert.Equal("items", Assert.IsType<PgDataType.Custom>(Assert.Single(composite.Attributes).DataType).Name.Name);
    }

    [Theory]
    [InlineData("CREATE TYPE details AS (Label TEXT, label TEXT)")]
    public void ToDefinition_RejectsDuplicateTypeMembers(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.ThrowsAny<SqlSyntaxException>(builder.ToDefinition);
    }

    [Theory]
    [InlineData("myattribute", QuoteStyle.None)]
    [InlineData("MyAttribute", QuoteStyle.Ansi)]
    public void ToDefinition_RejectsDuplicateCompositeAttributesInManuallyConstructedStatements(string name, QuoteStyle quoteStyle)
    {
        var builder = CreateBuilder();
        var parsed = Assert.IsType<CreateType.Composite>(ParseStatement("CREATE TYPE details AS (valid TEXT)"));
        var statement = parsed with
        {
            Attributes =
            [
                new CompositeTypeField(new Identifier(name, quoteStyle), new DataType.Text()),
                new CompositeTypeField(new Identifier(name, quoteStyle), new DataType.Int())
            ]
        };
        builder.ProcessStatements([statement], GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.DuplicateColumn>(builder.ToDefinition);
    }

    [Theory]
    [InlineData("happy")]
    [InlineData("")]
    [InlineData("it's fine")]
    public void ToDefinition_RejectsDuplicateEnumLabelsInManuallyConstructedStatements(string label)
    {
        var builder = CreateBuilder();
        var parsed = Assert.IsType<CreateType.Enumerated>(ParseStatement("CREATE TYPE mood AS ENUM ('valid')"));
        var statement = parsed with { Labels = [label, label] };
        builder.ProcessStatements([statement], GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(builder.ToDefinition);
    }

    [Fact]
    public void ToDefinition_PreservesCaseDistinctEnumLabels()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TYPE mood AS ENUM ('happy', 'Happy')"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var enumerated = Assert.IsType<PgCustomType.Enumerated>(Assert.Single(definition.CustomTypes).Value);
        Assert.Equal(new[] { "happy", "Happy" }, enumerated.Labels);
    }

    [Fact]
    public void ToDefinition_PreservesDistinctQuotedCompositeAttributes()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TYPE details AS (\"MyAttribute\" TEXT, myattribute TEXT)"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var composite = Assert.IsType<PgCustomType.Composite>(Assert.Single(definition.CustomTypes).Value);
        Assert.Equal(new[] { "MyAttribute", "myattribute" }, composite.Attributes.Select(attribute => attribute.Name.Name));
    }

    [Fact]
    public void ToDefinition_PreservesDomainAttributesAndCompositeCollations()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE DOMAIN Label AS TEXT COLLATE \"C\" DEFAULT 'x' CONSTRAINT Required NOT NULL CONSTRAINT \"Valid\" CHECK (VALUE <> ''); CREATE TYPE details AS (Label label COLLATE \"C\")"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var domain = Assert.IsType<PgCustomType.Domain>(definition.CustomTypes[new ObjectHandle("catalog", "public", "label")]);
        Assert.Equal("label", domain.Name.Name);
        Assert.Equal("\"C\"", domain.Collation?.ToSql());
        Assert.Equal("'x'", domain.Default?.ToSql());
        Assert.Equal(2, domain.Constraints.Count);
        var nullability = Assert.IsType<PgDomainConstraint.Nullability>(domain.Constraints[0]);
        Assert.True(nullability.NotNull);
        Assert.Equal("required", nullability.Name?.Name);
        var check = Assert.IsType<PgDomainConstraint.Check>(domain.Constraints[1]);
        Assert.Equal("Valid", check.Name?.Name);
        Assert.IsType<DomainValue>(Assert.IsType<Core.Expressions.BinaryOperator>(check.Expression).Left);
        var composite = Assert.IsType<PgCustomType.Composite>(definition.CustomTypes[new ObjectHandle("catalog", "public", "details")]);
        PgCompositeTypeField field = Assert.Single(composite.Attributes);
        Assert.Equal("label", field.Name.Name);
        Assert.Equal(domain.Name, Assert.IsType<PgDataType.Custom>(field.DataType).Name);
        Assert.Equal("\"C\"", field.Collation?.ToSql());
    }

    [Fact]
    public void ToDefinition_CanRetryAfterAForwardReferenceIsAdded()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (label label)"), GetSchema(), 0);
        Assert.Throws<SqlSyntaxException.TypeNotFound>(builder.ToDefinition);
        builder.ProcessStatements(ParseStatements("CREATE DOMAIN label AS TEXT"), GetSchema(), 1);

        PgDefinition first = builder.ToDefinition();
        PgDefinition second = builder.ToDefinition();

        Assert.Single(first.Tables);
        Assert.Single(first.CustomTypes);
        Assert.Equal(first.Tables, second.Tables);
        Assert.Equal(first.CustomTypes, second.CustomTypes);
    }

    [Fact]
    public void ToDefinition_ResolvesQuotedAndFoldedDeclarationsToDifferentTypes()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (first mood, second \"Mood\"); CREATE TYPE Mood AS ENUM ('a'); CREATE TYPE \"Mood\" AS ENUM ('b')"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var table = Assert.Single(definition.Tables).Value;
        Assert.Equal("mood", Assert.IsType<PgDataType.Custom>(table.Columns[0].DataType).Name.Name);
        Assert.Equal("Mood", Assert.IsType<PgDataType.Custom>(table.Columns[1].DataType).Name.Name);
    }

    [Fact]
    public void ToDefinition_HandlesLongAcyclicTypeChains()
    {
        const int length = 1024;
        string sql = string.Join("; ", Enumerable.Range(0, length).Select(index =>
            $"CREATE DOMAIN type_{index} AS {(index == length - 1 ? "TEXT" : $"type_{index + 1}")}"));
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(length, definition.CustomTypes.Count);
    }

    [Fact]
    public void ToDefinition_CycleDiagnosticExcludesAcyclicDependents()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE DOMAIN blocked AS first; CREATE DOMAIN first AS second; CREATE DOMAIN second AS first"), GetSchema(), 0);

        var exception = Assert.Throws<SqlSyntaxException.CyclicTypeDependency>(builder.ToDefinition);

        Assert.DoesNotContain("blocked", exception.Message);
        Assert.Contains("first", exception.Message);
        Assert.Contains("second", exception.Message);
        Assert.DoesNotContain(exception.Cycle, name => name.Name == "blocked");
    }

    [Fact]
    public void ToDefinition_CycleDiagnosticPreservesQuotedIdentityAndSourcedErrorContract()
    {
        const string sql = "CREATE DOMAIN \"Loop\" AS \"Loop\"";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 6);

        var exception = Assert.Throws<SqlSyntaxException.CyclicTypeDependency>(builder.ToDefinition);

        Assert.Equal("Loop", exception.Cycle[0].Name);
        Assert.Equal(QuoteStyle.Ansi, exception.Cycle[0].QuoteStyle);
        Assert.Equal(exception.Cycle[0], exception.Cycle[^1]);
        Assert.Contains("\"Loop\"", exception.Message);
        Assert.Equal(6, exception.ObjectSourceRef?.SourceId);
        Assert.Empty(exception.Data);
    }

    [Theory]
    [InlineData("CREATE DOMAIN label AS TEXT CONSTRAINT duplicate CHECK (VALUE <> '') CONSTRAINT Duplicate CHECK (VALUE <> 'x')")]
    public void ToDefinition_RejectsConflictingDomainConstraints(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.Throws<ValidationExceptionSet>(builder.ToDefinition);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ToDefinition_RejectsInvalidNullabilityInManuallyConstructedStatements(bool first, bool second)
    {
        var builder = CreateBuilder();
        var parsed = Assert.IsType<CreateDomain>(ParseStatement("CREATE DOMAIN label AS TEXT"));
        var statement = parsed with
        {
            Constraints =
            [
                new StatementDomainConstraint.Nullability(first),
                new StatementDomainConstraint.Nullability(second)
            ]
        };
        builder.ProcessStatements([statement], GetSchema(), 0);

        Assert.Throws<ValidationExceptionSet>(builder.ToDefinition);
    }

    [Fact]
    public void ToDefinition_AllowsRepeatedNullClauses()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE DOMAIN label AS TEXT CONSTRAINT optional NULL NULL CONSTRAINT optional CHECK (VALUE <> '')"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var domain = Assert.IsType<PgCustomType.Domain>(Assert.Single(definition.CustomTypes).Value);
        Assert.Equal(3, domain.Constraints.Count);
    }

    [Fact]
    public void ProcessStatements_RejectsTemporaryTablesWithoutLosingTheirSemantics()
    {
        var builder = CreateBuilder();

        var exception = Assert.Throws<DefinitionException.TempTable>(() => builder.ProcessStatements(ParseStatements("CREATE TEMP TABLE items (id INT)"), GetSchema(), 0));
        Assert.Contains("Temporary table", exception.Message);
        Assert.NotNull(exception.ObjectSourceRef);
    }

    [Fact]
    public void ProcessStatements_RejectsTableQueriesRatherThanDroppingThem()
    {
        var builder = CreateBuilder();

        Assert.Throws<DefinitionException.CreateTableAsSelect>(() => builder.ProcessStatements(ParseStatements("CREATE TABLE items AS SELECT 1"), GetSchema(), 0));
    }

    [Fact]
    public void Arrays_RoundTripWithoutFlatteningDomainOrElementIdentity()
    {
        const string sql = "CREATE TYPE mood AS ENUM ('happy'); "
            + "CREATE DOMAIN labels AS TEXT[3][4]; CREATE DOMAIN label AS TEXT; "
            + "CREATE TYPE details AS (values label[], moods mood ARRAY[3]); "
            + "CREATE TABLE items (payload details[], first labels, second label[][], numbers INT ARRAY[4] DEFAULT '{1,2}'); "
            + "CREATE FUNCTION echo(values label[]) RETURNS labels[] RETURN values; "
            + "CREATE PROCEDURE save(IN values mood[]) BEGIN ATOMIC SELECT values; END";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var domain = Assert.IsType<PgCustomType.Domain>(definition.CustomTypes[new ObjectHandle("catalog", "public", "labels")]);
        Assert.IsType<DataType.Text>(Assert.IsType<PgDataType.Array>(domain.BaseType).ElementType);
        PgTable table = Assert.Single(definition.Tables).Value;
        Assert.Equal("labels", Assert.IsType<PgDataType.Custom>(table.Columns[1].DataType).Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.IsType<PgDataType.Array>(table.Columns[2].DataType).ElementType).Name.Name);
        PgStoredFunction function = Assert.Single(definition.Functions).Value;
        Assert.Equal("labels", Assert.IsType<PgDataType.Custom>(Assert.IsType<PgDataType.Array>(Assert.IsType<FunctionReturn.DataTypeReturn>(function.ReturnType).DataType).ElementType).Name.Name);
        var statements = new List<Statement> { table.ToCreateStatement(true), function.ToCreateStatement(true), Assert.Single(definition.Procedures).Value.ToCreateStatement(true) };
        statements.AddRange(definition.CustomTypes.Values.Select(type => type.ToCreateStatement(true)));
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(string.Join("; ", statements.Select(statement => statement.ToSql()))), GetSchema(), 1);
        PgDefinition result = rebuilt.ToDefinition();
        Assert.Equal(definition.Tables, result.Tables);
        Assert.Equal(definition.CustomTypes, result.CustomTypes);
        Assert.Equal(function.Parameters, Assert.Single(result.Functions).Value.Parameters);
        Assert.Equal(function.ReturnType, Assert.Single(result.Functions).Value.ReturnType);
        Assert.Equal(Assert.Single(definition.Procedures).Value.Parameters, Assert.Single(result.Procedures).Value.Parameters);
    }

    [Fact]
    public void TableConstraints_ConstructNamedAndColumnLevelForms()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (Id INT PRIMARY KEY, label TEXT CONSTRAINT uq_label UNIQUE, amount INT CONSTRAINT ck_positive CHECK (amount > 0), CONSTRAINT uq_pair UNIQUE (label, amount), CONSTRAINT ck_amount CHECK (amount < 100))"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        PgTable table = Assert.Single(definition.Tables).Value;
        Assert.Equal(2, table.Checks.Count);
        Assert.Equal(2, table.UniqueConstraints.Count);
        Assert.Empty(table.Indexes);
        Assert.IsType<ColumnOption.Nullability.NotNull>(table.Columns[0].Nullability);
        Assert.Equal("id", Assert.Single(table.PrimaryKey!.Columns).Name);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(string.Join("; ", new[] { table.ToCreateStatement(true).ToSql() }
            .Concat(table.ToCreateIndexStatements(true).Select(statement => statement.ToSql())))), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, PRIMARY KEY (missing))")]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT uq_missing UNIQUE (missing))")]
    public void TableConstraints_RejectUnknownColumns(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.IndexColumnNotFound>(builder.ToDefinition);
    }

    [Fact]
    public void ForeignKeys_ResolveForwardTablesAndPreserveReferentialActions()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE child (id INT, parent_id INT, CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent(id) ON DELETE CASCADE ON UPDATE RESTRICT); CREATE TABLE parent (id INT PRIMARY KEY)"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        PgTable child = definition.Tables[new ObjectHandle("catalog", "public", "child")];
        Assert.Empty(child.Checks);
        PgTableConstraint.ForeignKey foreignKey = Assert.Single(child.ForeignKeys.Values);
        Assert.Equal("parent", foreignKey.ReferencedTable.Name);
        Assert.Equal(ReferentialAction.Cascade, foreignKey.OnDelete);
        Assert.Equal(ReferentialAction.Restrict, foreignKey.OnUpdate);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(string.Join("; ", definition.Tables.Values.Select(table => table.ToCreateStatement(true).ToSql()))), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
    }

    [Fact]
    public void Indexes_QueueForwardTablesAndEmitPostgresUsingPlacement()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE UNIQUE INDEX uq_parent ON parent USING BTREE (id); CREATE INDEX ix_child ON child (parent_id DESC) WHERE parent_id > 0; CREATE TABLE child (parent_id INT, CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent(id)); CREATE TABLE parent (id INT)"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        PgTable parent = definition.Tables[new ObjectHandle("catalog", "public", "parent")];
        Assert.True(Assert.Single(parent.Indexes.Values).Unique);
        Assert.Contains("ON \"public\".\"parent\" USING BTREE", Assert.Single(parent.ToCreateIndexStatements(true)).ToSql());
        var statements = new List<Statement>();
        foreach (PgTable table in definition.Tables.Values)
        {
            statements.AddRange(table.ToCreateIndexStatements(true));
            statements.Add(table.ToCreateStatement(true));
        }
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(string.Join("; ", statements.Select(statement => statement.ToSql()))), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
        Assert.Equal(definition.Tables, builder.ToDefinition().Tables);
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT PRIMARY KEY, PRIMARY KEY (id))")]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT key_name UNIQUE (id), CONSTRAINT Key_Name CHECK (id > 0))")]
    public void TableConstraints_RejectDuplicateKeysAndConstraintNames(string sql)
    {
        var builder = CreateBuilder();

        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() => builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, UNIQUE (id))")]
    [InlineData("CREATE TABLE items (id INT, FOREIGN KEY (id) REFERENCES items(id))")]
    [InlineData("CREATE TABLE items (id INT, CHECK (id > 0))")]
    public void TableConstraints_RequireNamesExceptPrimaryKey(string sql)
    {
        var builder = CreateBuilder();

        Assert.Throws<DefinitionException.TableConstraintNameRequired>(() =>
            builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT ck_missing CHECK (missing > 0))")]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items ((missing + 1))")]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items (id) WHERE missing > 0")]
    public void ConstraintAndIndexExpressions_RejectUnknownColumns(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(builder.ToDefinition);
    }

    [Theory]
    [InlineData("CREATE TABLE parent (id INT); CREATE TABLE child (id INT, CONSTRAINT fk_parent FOREIGN KEY (id) REFERENCES parent(id))")]
    [InlineData("CREATE TABLE parent (id INT); CREATE UNIQUE INDEX uq ON parent (id) WHERE id > 0; CREATE TABLE child (id INT, CONSTRAINT fk_parent FOREIGN KEY (id) REFERENCES parent(id))")]
    public void ForeignKeys_RejectMissingOrPartialBackingKeys(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        var exception = Assert.Throws<ValidationExceptionSet>(builder.ToDefinition);
        Assert.Contains(exception.Exceptions, error => error is DefinitionException.ForeignKeyNoReferencedBackingIndex);
    }

    [Theory]
    [InlineData("CREATE TABLE parent (id INT PRIMARY KEY); CREATE TABLE child (id INT, CONSTRAINT fk_parent FOREIGN KEY (id) REFERENCES parent(missing))", typeof(SqlSyntaxException.ForeignKeyReferencedColumnNotFound))]
    [InlineData("CREATE TABLE parent (id INT PRIMARY KEY); CREATE TABLE child (first INT, second INT, CONSTRAINT fk_parent FOREIGN KEY (first, second) REFERENCES parent(id))", typeof(SqlSyntaxException.ForeignKeyColumnCountMismatch))]
    public void ForeignKeys_ValidateReferencedColumnsAndCounts(string sql, Type expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        var exception = Assert.Throws<ValidationExceptionSet>(builder.ToDefinition);
        Assert.Contains(exception.Exceptions, error => error.GetType() == expected);
    }

    [Fact]
    public void ForeignKeys_RejectMissingReferencedTables()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE child (id INT, CONSTRAINT fk_missing FOREIGN KEY (id) REFERENCES missing(id))"), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.ForeignKeyReferencedTableNotFound>(builder.ToDefinition);
    }

    [Fact]
    public void Indexes_PreserveExpressionKeysAndQuotedColumns()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (\"Label\" TEXT, id INT, CONSTRAINT ck_id CHECK (items.id > 0)); CREATE INDEX ix ON items ((lower(\"Label\"))) WHERE id > 0"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        PgTable table = Assert.Single(definition.Tables).Value;
        Assert.IsType<KeyPart.IndexExpression>(Assert.Single(Assert.Single(table.Indexes.Values).Columns));
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements($"{table.ToCreateStatement(true).ToSql()}; {Assert.Single(table.ToCreateIndexStatements(true)).ToSql()}"), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items (id); CREATE INDEX IX ON items (id)")]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX items ON items (id)")]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT ix UNIQUE (id)); CREATE INDEX ix ON items (id)")]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT items UNIQUE (id))")]
    [InlineData("CREATE TABLE first (id INT, CONSTRAINT uq UNIQUE (id)); CREATE TABLE second (id INT, CONSTRAINT UQ UNIQUE (id))")]
    [InlineData("CREATE TABLE first (id INT, CONSTRAINT uq UNIQUE (id)); CREATE TABLE second (id INT); CREATE UNIQUE INDEX uq ON second (id)")]
    public void Indexes_RejectRelationNamespaceCollisions(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.DuplicateObject>(builder.ToDefinition);
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON missing (id)", typeof(SqlSyntaxException.CreateIndexUnknownTable))]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items (missing)", typeof(SqlSyntaxException.IndexColumnNotFound))]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items USING HASH (id DESC)", typeof(SqlSyntaxException.IndexDirectionNotSupported))]
    public void Indexes_RejectInvalidTablesColumnsAndOrdering(string sql, Type expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Exception exception = Assert.ThrowsAny<SqlSyntaxException>(builder.ToDefinition);
        Assert.Equal(expected, exception.GetType());
    }

    [Fact]
    public void Indexes_SupportBasicHashIndexes()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE INDEX ix ON items USING HASH (id)"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        PgTable table = Assert.Single(definition.Tables).Value;
        Assert.Equal(IndexMethod.Hash, Assert.Single(table.Indexes.Values).Method);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements($"{table.ToCreateStatement(true).ToSql()}; {Assert.Single(table.ToCreateIndexStatements(true)).ToSql()}"), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
    }

    [Fact]
    public void ForeignKeys_AreStoredSeparatelyFromChecksAndRoundTrip()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT PRIMARY KEY, CONSTRAINT ck_id CHECK (id > 0), CONSTRAINT fk_self FOREIGN KEY (id) REFERENCES items(id))"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();
        PgTable table = Assert.Single(definition.Tables).Value;
        Assert.Equal("ck_id", Assert.Single(table.Checks.Values).Name!.Name);
        Assert.Equal("fk_self", Assert.Single(table.ForeignKeys.Values).Name!.Name);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(table.ToCreateStatement(true).ToSql()), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
    }

    [Theory]
    [InlineData("CONSTRAINT same CHECK (id > 0), CONSTRAINT SAME FOREIGN KEY (id) REFERENCES items(id)")]
    [InlineData("CONSTRAINT same FOREIGN KEY (id) REFERENCES items(id), CONSTRAINT SAME CHECK (id > 0)")]
    public void ForeignKeys_ShareConstraintNameValidationWithChecks(string constraints)
    {
        var builder = CreateBuilder();

        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() => builder.ProcessStatements(
            ParseStatements($"CREATE TABLE items (id INT PRIMARY KEY, {constraints})"), GetSchema(), 0));
    }

    [Theory]
    [InlineData("CREATE TABLE items (Id INT CONSTRAINT uq UNIQUE, \"Payload\" TEXT)", false)]
    [InlineData("CREATE TABLE items (Id INT, \"Payload\" TEXT, CONSTRAINT uq UNIQUE (Id))", false)]
    [InlineData("CREATE TABLE items (Id INT, \"Payload\" TEXT, CONSTRAINT uq UNIQUE (Id) INCLUDE (\"Payload\"))", true)]
    public void UniqueConstraints_PreserveIdentityAndRoundTrip(string sql, bool includesPayload)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();
        PgTable table = Assert.Single(definition.Tables).Value;
        PgTableConstraint.Unique unique = Assert.Single(table.UniqueConstraints.Values);
        Assert.Equal("uq", unique.Name!.Name);
        Assert.Equal("id", Assert.Single(unique.Columns).Name);
        Assert.Empty(table.Indexes);
        Assert.Empty(table.ToCreateIndexStatements(true));
        CreateTable statement = table.ToCreateStatement(true);
        Assert.IsType<StatementTableConstraint.UniqueConstraint>(Assert.Single(statement.Constraints));
        if (includesPayload)
        {
            Assert.Equal("Payload", Assert.Single(unique.IncludedColumns!.Columns).Name);
            Assert.Contains("INCLUDE (\"Payload\")", statement.ToSql());
        }
        else
        {
            Assert.Null(unique.IncludedColumns);
        }
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(statement.ToSql()), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
    }

    [Fact]
    public void UniqueConstraints_AreDistinctFromEquivalentStandaloneIndexes()
    {
        var constraintBuilder = CreateBuilder();
        constraintBuilder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT, CONSTRAINT uq UNIQUE (id))"), GetSchema(), 0);
        var indexBuilder = CreateBuilder();
        indexBuilder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE UNIQUE INDEX uq ON items (id)"), GetSchema(), 0);

        PgTable constraintTable = Assert.Single(constraintBuilder.ToDefinition().Tables).Value;
        PgTable indexTable = Assert.Single(indexBuilder.ToDefinition().Tables).Value;
        Assert.NotEqual(constraintTable, indexTable);
        Assert.Single(constraintTable.UniqueConstraints);
        Assert.Empty(constraintTable.Indexes);
        Assert.Empty(indexTable.UniqueConstraints);
        Assert.True(Assert.Single(indexTable.Indexes.Values).Unique);
    }

    [Fact]
    public void UniqueConstraints_RejectRepeatedKeyColumns()
    {
        var builder = CreateBuilder();
        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() => builder.ProcessStatements(
            ParseStatements("CREATE TABLE items (id INT, CONSTRAINT uq UNIQUE (id, ID))"), GetSchema(), 0));
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, \"Payload\" TEXT); CREATE UNIQUE INDEX ix ON items (id) INCLUDE (\"Payload\")", true)]
    [InlineData("CREATE TABLE items (id INT, \"Payload\" TEXT); CREATE INDEX ix ON items (id) INCLUDE (\"Payload\")", false)]
    public void Indexes_PreserveIncludedColumnsAndRoundTrip(string sql, bool unique)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();
        PgTable table = Assert.Single(definition.Tables).Value;
        PgIndex index = Assert.Single(table.Indexes.Values);
        Assert.Equal(unique, index.Unique);
        Assert.Equal("id", Assert.IsType<KeyPart.Column>(Assert.Single(index.Columns)).Name.Name);
        Assert.Equal("Payload", Assert.Single(index.IncludedColumns!.Columns).Name);
        Assert.Contains("INCLUDE (\"Payload\")", Assert.Single(table.ToCreateIndexStatements(true)).ToSql());
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements($"{table.ToCreateStatement(true).ToSql()}; {Assert.Single(table.ToCreateIndexStatements(true)).ToSql()}"), GetSchema(), 1);
        Assert.Equal(definition.Tables, rebuilt.ToDefinition().Tables);
    }

    [Theory]
    [InlineData("CONSTRAINT ix UNIQUE (id) INCLUDE (missing)", typeof(SqlSyntaxException.IndexColumnNotFound))]
    [InlineData("CONSTRAINT ix UNIQUE (id) INCLUDE (payload, PAYLOAD)", typeof(SqlSyntaxException.SpecifiedMoreThanOnce))]
    [InlineData("CONSTRAINT ix UNIQUE (id) INCLUDE (id)", typeof(SqlSyntaxException.SpecifiedMoreThanOnce))]
    public void Indexes_ValidateIncludedColumns(string constraint, Type expected)
    {
        var builder = CreateBuilder();
        Exception exception = Assert.ThrowsAny<SqlSyntaxException>(() =>
        {
            builder.ProcessStatements(ParseStatements($"CREATE TABLE items (id INT, payload TEXT, {constraint})"), GetSchema(), 0);
            builder.ToDefinition();
        });
        Assert.Equal(expected, exception.GetType());
    }

    [Fact]
    public void Indexes_RejectIncludeOnHashIndexes()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT, payload TEXT); CREATE INDEX ix ON items USING HASH (id) INCLUDE (payload)"), GetSchema(), 0);

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);
        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Equal("INCLUDE with HASH indexes", exception.Clause);
        Assert.NotNull(exception.ObjectSourceRef);
    }

    [Theory]
    [InlineData("id", true, true)]
    [InlineData("payload", false, true)]
    [InlineData("id", true, false)]
    [InlineData("payload", false, false)]
    public void ForeignKeys_UseOnlyKeyColumnsOfCoveringUniqueKeys(string referencedColumn, bool backed, bool constraint)
    {
        var builder = CreateBuilder();
        string parentSql = constraint
            ? "CREATE TABLE parent (id INT, payload INT, CONSTRAINT uq UNIQUE (id) INCLUDE (payload))"
            : "CREATE TABLE parent (id INT, payload INT); CREATE UNIQUE INDEX uq ON parent (id) INCLUDE (payload)";
        builder.ProcessStatements(ParseStatements($"{parentSql}; CREATE TABLE child (id INT, CONSTRAINT fk_parent FOREIGN KEY (id) REFERENCES parent({referencedColumn}))"), GetSchema(), 0);

        if (backed)
        {
            Assert.Single(builder.ToDefinition().Tables[new ObjectHandle("catalog", "public", "child")].ForeignKeys);
        }
        else
        {
            var exception = Assert.Throws<ValidationExceptionSet>(builder.ToDefinition);
            Assert.Contains(exception.Exceptions, error => error is DefinitionException.ForeignKeyNoReferencedBackingIndex);
        }
    }

    [Theory]
    [InlineData("method", "USING HASH")]
    [InlineData("organization", "CLUSTERED")]
    [InlineData("options", "PAD_INDEX")]
    [InlineData("storage", "ON filegroup")]
    [InlineData("comment", "inline COMMENT")]
    public void PrimaryKeys_ReportSpecificUnsupportedIndexFeatures(string feature, string message)
    {
        var builder = CreateBuilder();
        var table = Assert.IsType<CreateTable>(Assert.Single(ParseStatements("CREATE TABLE items (id INT, PRIMARY KEY (id))")));
        var primaryKey = Assert.IsType<StatementTableConstraint.PrimaryKey>(Assert.Single(table.Constraints));
        StatementTableConstraint.PrimaryKey invalid = feature switch
        {
            "method" => primaryKey with { IndexMethod = IndexMethod.Hash },
            "organization" => primaryKey with { IndexOrganization = IndexOrganization.Clustered },
            "options" => primaryKey with { Options = [new StatementIndexOption.PadIndex(true)] },
            "storage" => primaryKey with { StorageLocation = new IndexStorageLocation.Filegroup(new Identifier("filegroup")) },
            "comment" => primaryKey with { Comment = new Comment("comment") },
            _ => throw new InvalidOperationException()
        };

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.ProcessStatements(
            [table with { Constraints = [invalid] }], GetSchema(), 0));
        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Equal(new SourceRef(0, table.Meta.Start!.Value, table.Meta.End!.Value), exception.ObjectSourceRef);
        Assert.Contains(message, exception.Clause);
        Assert.Contains(message, exception.Message);
    }

    [Fact]
    public void UniqueConstraints_RejectSeparateIndexNamesSpecifically()
    {
        var builder = CreateBuilder();
        var table = Assert.IsType<CreateTable>(Assert.Single(ParseStatements("CREATE TABLE items (id INT, CONSTRAINT uq UNIQUE (id))")));
        var unique = Assert.IsType<StatementTableConstraint.UniqueConstraint>(Assert.Single(table.Constraints));

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.ProcessStatements(
            [table with { Constraints = [unique with { IndexName = new Identifier("ix") }] }], GetSchema(), 0));
        Assert.Equal("separate UNIQUE index name", exception.Clause);
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT, PRIMARY KEY (id) WITH (FILLFACTOR = 70))")]
    [InlineData("CREATE TABLE items (id INT, CONSTRAINT uq UNIQUE (id) WITH (FILLFACTOR = 70))")]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items (id) WITH (FILLFACTOR = 70)")]
    [InlineData("CREATE TABLE items (id INT); CREATE UNIQUE INDEX ix ON items (id) WITH (FILLFACTOR = 70)")]
    public void IndexOptions_ClassifyFillFactorAsNotImplemented(string sql)
    {
        var builder = CreateBuilder();

        var exception = Assert.Throws<NotImplementedException>(() =>
        {
            builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);
            builder.ToDefinition();
        });
        Assert.Contains("FILLFACTOR", exception.Message);
    }

    [Theory]
    [InlineData("PAD_INDEX")]
    [InlineData("IGNORE_DUP_KEY")]
    [InlineData("STATISTICS_NORECOMPUTE")]
    [InlineData("STATISTICS_INCREMENTAL")]
    [InlineData("ALLOW_ROW_LOCKS")]
    [InlineData("ALLOW_PAGE_LOCKS")]
    [InlineData("OPTIMIZE_FOR_SEQUENTIAL_KEY")]
    public void IndexOptions_RejectForeignOptionsWithDialectSyntaxError(string option)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (id INT); CREATE INDEX ix ON items (id) WITH ({option} = ON)"), GetSchema(), 7);

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);
        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Equal($"{option} = ON", exception.Clause);
        Assert.NotNull(exception.ObjectSourceRef);
        Assert.Contains("does not support the clause", exception.Message);
    }

    [Theory]
    [InlineData("PRIMARY KEY (id)")]
    [InlineData("CONSTRAINT uq UNIQUE (id)")]
    [InlineData("CONSTRAINT fk FOREIGN KEY (id) REFERENCES items(id)")]
    [InlineData("CONSTRAINT ck CHECK (id > 0)")]
    public void TableConstraints_RejectInlineCommentsWithDialectSyntaxError(string constraint)
    {
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.ProcessStatements(
            ParseStatements($"CREATE TABLE items (id INT, {constraint} COMMENT 'description')"), GetSchema(), 0));
        Assert.Equal("inline COMMENT", exception.Clause);
        Assert.NotNull(exception.ObjectSourceRef);
    }

    [Theory]
    [InlineData("Mixed.Items", "mixed", "items")]
    [InlineData("\"Mixed\".\"Items\"", "Mixed", "Items")]
    public void GetAndValidateName_ReturnsCanonicalIdentifierAndOriginalHandle(string sqlName, string schemaName, string objectName)
    {
        var statement = Assert.IsType<CreateTable>(Assert.Single(ParseStatements($"CREATE TABLE {sqlName} (id INT)")));
        var builder = new NameInspectingBuilder(PgTestConfig.GetTestConfig());

        ObjectNameAndHandle result = builder.GetName(statement, GetSchema());

        Assert.Equal(schemaName, result.Id.Schema.Name);
        Assert.Equal(objectName, result.Id.Name);
        Assert.Equal(QuoteStyle.Ansi, result.Id.QuoteStyle);
        Assert.Equal(QuoteStyle.Ansi, result.Id.Schema.QuoteStyle);
        Assert.Equal(QuoteStyle.Ansi, result.Id.Schema.Catalog.QuoteStyle);
        Assert.Equal(statement.Name.Values, result.Id.OriginalIdentifiers);
        Assert.Equal(ObjectHandle.Create(statement.Name, GetSchema(), NameHandling.LowercaseUnlessQuoted), result.Key);
        Assert.Throws<SqlSyntaxException.DuplicateObject>(() => builder.GetName(statement, GetSchema()));
    }

    [Fact]
    public void GetAndValidateName_PreservesUnquotedAndQuotedCaseDistinctHandles()
    {
        var builder = new NameInspectingBuilder(PgTestConfig.GetTestConfig());
        var statements = ParseStatements("CREATE TABLE Items (id INT); CREATE TABLE \"Items\" (id INT)");

        ObjectNameAndHandle unquoted = builder.GetName(Assert.IsType<CreateTable>(statements[0]), GetSchema());
        ObjectNameAndHandle quoted = builder.GetName(Assert.IsType<CreateTable>(statements[1]), GetSchema());

        Assert.Equal("items", unquoted.Id.Name);
        Assert.Equal("Items", quoted.Id.Name);
        Assert.NotEqual(unquoted.Key, quoted.Key);
    }

    [Theory]
    [InlineData("INT", typeof(FunctionReturn.DataTypeReturn))]
    [InlineData("trigger", typeof(FunctionReturn.PseudoType))]
    [InlineData("void", typeof(FunctionReturn.PseudoType))]
    [InlineData("record", typeof(FunctionReturn.PseudoType))]
    [InlineData("SETOF INT", typeof(FunctionReturn.SetOf))]
    [InlineData("SETOF label[]", typeof(FunctionReturn.SetOf))]
    [InlineData("SETOF items", typeof(FunctionReturn.SetOf))]
    [InlineData("SETOF record", typeof(FunctionReturn.SetOf))]
    [InlineData("SETOF void", typeof(FunctionReturn.SetOf))]
    [InlineData("TABLE (Id INT, \"Label\" label[])", typeof(FunctionReturn.Table))]
    public void FunctionReturnShapes_ResolveAndRoundTrip(string returnClause, Type expectedType)
    {
        const string declarations = "CREATE DOMAIN label AS TEXT; CREATE TABLE items (id INT)";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"{declarations}; CREATE FUNCTION f() RETURNS {returnClause} LANGUAGE plpgsql AS $$opaque body$$"), GetSchema(), 0);

        PgStoredFunction function = Assert.Single(builder.ToDefinition().Functions).Value;
        Assert.IsType(expectedType, function.ReturnType);
        CreateFunction statement = function.ToCreateStatement(true);
        Assert.Equal(function.ReturnType.ToStatementReturn(), statement.ReturnType);
        Assert.Equal(statement.ReturnType!.ToSql(), function.ReturnType.ToSql());
        Assert.Equal("opaque body", Assert.IsType<RoutineBodyLiteral>(function.Body).Value);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements($"{declarations}; {statement.ToSql()}"), GetSchema(), 1);
        Assert.Equal(function.ReturnType, Assert.Single(rebuilt.ToDefinition().Functions).Value.ReturnType);
    }

    [Fact]
    public void FunctionTableReturn_NormalizesNamesAndPreservesResolvedArrayElements()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE DOMAIN label AS TEXT; CREATE FUNCTION f() RETURNS TABLE (Id INT, \"Label\" label[]) LANGUAGE plpgsql AS 'opaque'"), GetSchema(), 0);

        var result = Assert.IsType<FunctionReturn.Table>(Assert.Single(builder.ToDefinition().Functions).Value.ReturnType);

        Assert.Equal(["id", "Label"], result.Columns.Select(column => column.Name.Name));
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.IsType<PgDataType.Array>(result.Columns[1].DataType).ElementType).Name.Name);
    }

    [Theory]
    [InlineData("Id INT, id TEXT")]
    [InlineData("id INT, \"id\" TEXT")]
    [InlineData("\"Id\" INT, \"Id\" TEXT")]
    public void FunctionTableReturn_DuplicateNames_AreRejectedWithSource(string columns)
    {
        string sql = $"CREATE FUNCTION f() RETURNS TABLE ({columns}) LANGUAGE plpgsql AS 'opaque'";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 7);

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(builder.ToDefinition);

        Assert.Equal(7, exception.ObjectSourceRef?.SourceId);
        Assert.Contains("return column", exception.Message);
        Assert.Contains("function", exception.Message);
        Assert.True(exception.ObjectSourceRef?.Start > sql.IndexOf(',', StringComparison.Ordinal));
    }

    [Fact]
    public void FunctionTableReturn_QuotedCaseDistinctNames_ArePreserved()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE FUNCTION f() RETURNS TABLE (id INT, \"Id\" TEXT) LANGUAGE plpgsql AS 'opaque'"), GetSchema(), 0);

        var result = Assert.IsType<FunctionReturn.Table>(Assert.Single(builder.ToDefinition().Functions).Value.ReturnType);

        Assert.Equal(["id", "Id"], result.Columns.Select(column => column.Name.Name));
    }

    [Fact]
    public void FunctionTableReturn_ManualDuplicateUsesRoutineSource()
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement("CREATE FUNCTION f() RETURNS TABLE (id INT, id TEXT) LANGUAGE plpgsql AS 'opaque'"));
        var table = Assert.IsType<StatementFunctionReturn.Table>(function.ReturnType);
        function = function with
        {
            ReturnType = table with { Columns = [.. table.Columns.Select(column => column with { Name = column.Name with { Source = null } })] }
        };
        var builder = CreateBuilder();
        builder.ProcessStatements([function], GetSchema(), 7);

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(builder.ToDefinition);

        Assert.Equal(new SourceRef(7, function.Meta.Start!.Value, function.Meta.End!.Value), exception.ObjectSourceRef);
    }

    [Fact]
    public void FunctionReturn_UnknownManualVariant_IsExplicitlyRejected()
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement("CREATE FUNCTION f() RETURNS INT RETURN 1")) with
        {
            ReturnType = new UnknownFunctionReturn()
        };
        var builder = CreateBuilder();
        builder.ProcessStatements([function], GetSchema(), 0);

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);
        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Contains("function return shape", exception.Clause);
    }

    [Fact]
    public void TriggerModel_EmitsCanonicalFunctionAndStringArguments()
    {
        ObjectIdentifier table = ObjectIdentifier.FromStrings("catalog", "app", "items");
        var trigger = new PgTrigger(ObjectIdentifier.FromStrings("catalog", "app", "audit"), table,
            TriggerTime.After, [new TriggerEvent.Insert()], ObjectIdentifier.FromStrings("catalog", "app", "audit_fn"), ["named", "1", "it's ok"]);

        Assert.Equal("CREATE TRIGGER audit AFTER INSERT ON app.items FOR EACH STATEMENT EXECUTE FUNCTION app.audit_fn('named', '1', 'it''s ok')",
            trigger.ToCreateStatement(true).ToSql());
        Assert.Equal("CREATE TRIGGER audit AFTER INSERT ON items FOR EACH STATEMENT EXECUTE FUNCTION audit_fn('named', '1', 'it''s ok')",
            trigger.ToCreateStatement(false).ToSql());
        Assert.Equal(trigger, Serialization.FromJson<PgTrigger>(Serialization.ToJson(trigger)));
    }

    [Fact]
    public void ToDefinition_ResolvesForwardTriggerReferencesAndDefaults()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TRIGGER audit AFTER INSERT ON app.items EXECUTE PROCEDURE app.audit_fn(Named, \"Quoted\", 01, 1.5, 'it''s ok'); CREATE TABLE app.items (id INT); CREATE FUNCTION app.audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'"), GetSchema(), 8);

        PgTrigger trigger = Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values);

        Assert.Equal("app", trigger.Name.Schema.Name);
        Assert.Equal("items", trigger.OnTable.Name);
        Assert.Equal("audit_fn", trigger.FunctionName.Name);
        Assert.Equal(TriggerForEachType.Statement, trigger.ForEachType);
        Assert.Equal(["named", "Quoted", "1", "1.5", "it's ok"], trigger.Arguments);
        Assert.Null(trigger.ConstraintOptions);
        var body = Assert.IsType<PgTriggerExecute>(trigger.ToCreateStatement(true).Body);
        Assert.False(body.UseProcedure);
    }

    [Theory]
    [InlineData("EXISTS (SELECT 1)")]
    [InlineData("NEW.id IN (SELECT 1)")]
    [InlineData("NEW.id = (SELECT 1)")]
    [InlineData("OLD.id > 0")]
    [InlineData("NEW.missing > 0")]
    [InlineData("other.id > 0")]
    [InlineData("NEW.id > 0 AND EXISTS (SELECT 1)")]
    [InlineData("coalesce((SELECT 1), 0) > 0")]
    [InlineData("EXISTS (SELECT 1 LIMIT 1) IS FALSE")]
    [InlineData("(EXISTS (SELECT 1)) IS TRUE")]
    [InlineData("(EXISTS (SELECT 1)) IS UNKNOWN")]
    [InlineData("NEW.id IS DISTINCT FROM (SELECT 1)")]
    [InlineData("NEW.id::INT = (SELECT 1)")]
    [InlineData("(NEW.id, (SELECT 1)) IS NOT NULL")]
    [InlineData("CASE WHEN TRUE THEN EXISTS (SELECT 1) ELSE FALSE END")]
    [InlineData("NEW.id = ANY((SELECT 1))")]
    [InlineData("NEW.id LIKE OLD.id")]
    [InlineData("NEW.id ILIKE OLD.id")]
    [InlineData("NEW.id SIMILAR TO OLD.id")]
    [InlineData("NEW.id AT TIME ZONE OLD.id IS NOT NULL")]
    [InlineData("POSITION(OLD.id IN NEW.id) > 0")]
    [InlineData("allowed(*)")]
    [InlineData("allowed(OLD.*)")]
    [InlineData("NEW.id.extra > 0")]
    [InlineData("allowed(public.NEW.*)")]
    [InlineData("sum(DISTINCT NEW.id) > 0")]
    [InlineData("sum(NEW.id ORDER BY NEW.id) > 0")]
    [InlineData("\"NEW\".id > 0")]
    public void ToDefinition_RejectsInvalidTriggerWhenWithSource(string when)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit AFTER INSERT ON items FOR ROW WHEN ({when}) EXECUTE FUNCTION audit_fn()"), GetSchema(), 12);

        var exception = Assert.Throws<SqlSyntaxException.InvalidTrigger>(builder.ToDefinition);

        Assert.Contains("WHEN", exception.Clause);
        Assert.Equal(12, exception.ObjectSourceRef?.SourceId);
    }

    [Fact]
    public void ToDefinition_ResolvesAndNormalizesTriggerWhenRows()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("""
        CREATE TABLE items (id INT);

        CREATE FUNCTION audit_fn()
            RETURNS trigger LANGUAGE plpgsql AS 'opaque';

        CREATE TRIGGER audit
            AFTER UPDATE ON items
            FOR EACH ROW WHEN (NEW.ID IS DISTINCT FROM OLD.ID)
            EXECUTE FUNCTION audit_fn()
        """), GetSchema(), 0);

        PgTrigger trigger = Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values);
        var when = Assert.IsType<IsDistinctFrom>(trigger.When);

        Assert.Equal(["new", "id"], Assert.IsType<CompoundIdentifier>(when.Left).Identifiers.Select(identifier => identifier.Name));
        Assert.Equal(["old", "id"], Assert.IsType<CompoundIdentifier>(when.Right).Identifiers.Select(identifier => identifier.Name));
    }

    [Theory]
    [InlineData("NOW() IS NOT NULL", "NOW() IS NOT NULL")]
    [InlineData("NEW.ID::INT > 0", "\"new\".\"id\"::INT > 0")]
    [InlineData("CAST(NEW.ID AS INT) > 0", "CAST(\"new\".\"id\" AS INT) > 0")]
    [InlineData("(NEW.ID + 1) > 0", "(\"new\".\"id\" + 1) > 0")]
    [InlineData("NEW.ID IS TRUE", "\"new\".\"id\" IS TRUE")]
    [InlineData("NEW.ID IS UNKNOWN", "\"new\".\"id\" IS UNKNOWN")]
    [InlineData("NEW.ID = ANY(NEW.ID)", "\"new\".\"id\" = ANY(\"new\".\"id\")")]
    [InlineData("(NEW.ID, NEW.ID) IS NOT NULL", "(\"new\".\"id\", \"new\".\"id\") IS NOT NULL")]
    [InlineData("allowed(NEW.*)", "allowed(\"new\".*)")]
    [InlineData("allowed(NEW)", "allowed(\"new\")")]
    [InlineData("NEW.ctid IS NOT NULL", "\"new\".\"ctid\" IS NOT NULL")]
    public void ToDefinition_NormalizesTriggerWhenUsingSharedTraversal(string expression, string expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"""
            CREATE TABLE items (id INT);

            CREATE FUNCTION audit_fn()
                RETURNS trigger
                LANGUAGE plpgsql
                AS 'opaque';

            CREATE TRIGGER audit
                AFTER INSERT ON items
                FOR ROW WHEN ({expression})
                EXECUTE FUNCTION audit_fn()
            """), GetSchema(), 0);

        PgTrigger trigger = Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values);

        Assert.Equal(expected, trigger.When!.ToSql());
    }

    [Theory]
    [InlineData("CREATE TRIGGER audit BEFORE INSERT ON items EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE TRIGGER audit AFTER TRUNCATE ON items EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE TRIGGER audit AFTER UPDATE OF id ON items FOR ROW EXECUTE PROCEDURE audit_fn()")]
    [InlineData("CREATE TRIGGER audit AFTER UPDATE ON items REFERENCING NEW TABLE new_rows OLD TABLE old_rows FOR STATEMENT EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items REFERENCING NEW TABLE new_rows FOR ROW EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE TRIGGER audit AFTER DELETE ON items REFERENCING OLD TABLE old_rows EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE TRIGGER audit BEFORE INSERT ON items FOR ROW WHEN (NEW.id > 0 AND NEW IS NOT NULL) EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER UPDATE ON items FOR EACH ROW EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER UPDATE ON items FROM other INITIALLY DEFERRED FOR ROW EXECUTE PROCEDURE audit_fn()")]
    public void ToDefinition_CanonicalTriggersResolveIdempotently(string sql)
    {
        const string declarations = "CREATE TABLE items (id INT); CREATE TABLE other (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; ";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(declarations + sql), GetSchema(), 0);
        PgTrigger trigger = Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values);
        var reloaded = CreateBuilder();

        reloaded.ProcessStatements(ParseStatements(declarations + trigger.ToCreateStatement(true).ToSql()), GetSchema(), 0);

        Assert.Equal(trigger, Assert.Single(Assert.Single(reloaded.ToDefinition().Triggers.Values).Values));
        Assert.Equal(trigger, Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values));
    }

    [Theory]
    [InlineData("INSTEAD OF INSERT ON items FOR ROW")]
    [InlineData("AFTER TRUNCATE ON items FOR ROW")]
    [InlineData("AFTER INSERT ON items REFERENCING OLD TABLE old_rows")]
    [InlineData("AFTER DELETE ON items REFERENCING NEW TABLE new_rows")]
    [InlineData("AFTER TRUNCATE ON items REFERENCING NEW TABLE new_rows")]
    [InlineData("BEFORE INSERT ON items REFERENCING NEW TABLE new_rows")]
    [InlineData("AFTER INSERT OR UPDATE ON items REFERENCING NEW TABLE new_rows")]
    [InlineData("AFTER UPDATE OF id ON items REFERENCING NEW TABLE new_rows")]
    [InlineData("AFTER UPDATE ON items REFERENCING OLD TABLE same_rows NEW TABLE same_rows")]
    [InlineData("AFTER UPDATE ON items REFERENCING OLD TABLE old_rows OLD TABLE other_rows")]
    [InlineData("AFTER UPDATE ON items REFERENCING NEW TABLE new_rows NEW TABLE other_rows")]
    [InlineData("AFTER UPDATE OF missing ON items")]
    [InlineData("AFTER INSERT ON items WHEN (NEW.id > 0)")]
    [InlineData("AFTER DELETE ON items FOR ROW WHEN (NEW.id > 0)")]
    [InlineData("BEFORE INSERT ON items FOR ROW WHEN (NEW.ctid IS NOT NULL)")]
    [InlineData("BEFORE INSERT ON items FOR ROW WHEN (NEW.generated > 0)")]
    [InlineData("BEFORE INSERT ON items FOR ROW WHEN (NEW IS NOT NULL)")]
    [InlineData("AFTER INSERT ON items FOR ROW WHEN (TRUE IN (SELECT TRUE))")]
    [InlineData("AFTER UPDATE ON items FOR ROW WHEN (sum(NEW.id) OVER () > 0)")]
    public void ToDefinition_RejectsInvalidTriggerCombinations(string clauses)
    {
        var builder = CreateBuilder();
        var statements = ParseStatements("CREATE TABLE items (id INT, generated INT GENERATED ALWAYS AS (id + 1) STORED); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit " + clauses + " EXECUTE FUNCTION audit_fn()");
        builder.ProcessStatements(statements, GetSchema(), 21);

        var exception = Assert.Throws<SqlSyntaxException.InvalidTrigger>(builder.ToDefinition);

        Assert.Equal("audit", exception.Trigger.Name);
        Assert.Equal(new SourceRef(21, statements[^1].Meta.Start!.Value, statements[^1].Meta.End!.Value), exception.ObjectSourceRef);
    }

    [Theory]
    [InlineData("BEFORE INSERT ON items FOR ROW")]
    [InlineData("AFTER INSERT ON items")]
    [InlineData("AFTER TRUNCATE ON items FOR ROW")]
    [InlineData("AFTER UPDATE ON items NOT DEFERRABLE INITIALLY DEFERRED FOR ROW")]
    [InlineData("AFTER UPDATE ON items REFERENCING NEW TABLE new_rows FOR ROW")]
    public void ToDefinition_RejectsInvalidConstraintTriggers(string clauses)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE CONSTRAINT TRIGGER audit " + clauses + " EXECUTE FUNCTION audit_fn()"), GetSchema(), 22);

        var exception = Assert.Throws<SqlSyntaxException.InvalidTrigger>(builder.ToDefinition);

        Assert.Equal(22, exception.ObjectSourceRef?.SourceId);
    }

    [Theory]
    [InlineData("AFTER INSERT OR INSERT ON items")]
    [InlineData("AFTER UPDATE OF id, ID ON items")]
    public void ToDefinition_RejectsDuplicateTriggerAttributes(string clauses)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit " + clauses + " EXECUTE FUNCTION audit_fn()"), GetSchema(), 23);

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(builder.ToDefinition);

        Assert.Equal(23, exception.ObjectSourceRef?.SourceId);
    }

    [Fact]
    public void ToDefinition_TriggerNamesAreScopedToTablesNotSchemas()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE audit (id INT); CREATE TABLE other (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit AFTER INSERT ON audit EXECUTE FUNCTION audit_fn(); CREATE TRIGGER audit AFTER INSERT ON other EXECUTE FUNCTION audit_fn(); CREATE TRIGGER \"Audit\" AFTER INSERT ON audit EXECUTE FUNCTION audit_fn()"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(2, definition.Triggers.Count);
        ObjectIdentifier auditTable = Assert.Single(definition.Tables.Values, table => table.Name.Name == "audit").Name;
        ObjectIdentifier otherTable = Assert.Single(definition.Tables.Values, table => table.Name.Name == "other").Name;
        DatabaseComponentDict<PgTrigger> auditTriggers = definition.Triggers[auditTable];
        Assert.Same(auditTriggers, definition.Triggers[new ObjectIdentifier("AUDIT", auditTable.Schema, QuoteStyle.None)]);
        Assert.Equal(2, auditTriggers.Count);
        Assert.Equal("audit", auditTriggers[new Identifier("AUDIT")].Name.Name);
        Assert.Equal("Audit", auditTriggers[new Identifier("Audit", QuoteStyle.Ansi)].Name.Name);
        Assert.Equal("audit", Assert.Single(definition.Triggers[otherTable].Values).Name.Name);
    }

    [Fact]
    public void ToDefinition_TriggerGroupsAreOrderIndependentAndOmitTablesWithoutTriggers()
    {
        const string declarations = "CREATE TABLE items (id INT); CREATE TABLE other (id INT); CREATE TABLE unused (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; ";
        string[] statements =
        [
            "CREATE TRIGGER first AFTER INSERT ON items EXECUTE FUNCTION audit_fn()",
            "CREATE TRIGGER second AFTER UPDATE ON items EXECUTE FUNCTION audit_fn()",
            "CREATE TRIGGER first AFTER INSERT ON other EXECUTE FUNCTION audit_fn()"
        ];
        var builder = CreateBuilder();
        var reversed = CreateBuilder();
        builder.ProcessStatements(ParseStatements(declarations + string.Join("; ", statements)), GetSchema(), 0);
        reversed.ProcessStatements(ParseStatements(declarations + string.Join("; ", statements.Reverse())), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(definition.Triggers, reversed.ToDefinition().Triggers);
        Assert.Equal(2, definition.Triggers.Count);
        ObjectIdentifier unusedTable = Assert.Single(definition.Tables.Values, table => table.Name.Name == "unused").Name;
        Assert.False(definition.Triggers.ContainsKey(unusedTable));
    }

    [Fact]
    public void ToDefinition_RejectsDuplicateTriggerNamesOnSameTable()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit_fn(); CREATE TRIGGER AUDIT AFTER UPDATE ON items EXECUTE FUNCTION audit_fn()"), GetSchema(), 24);

        var exception = Assert.Throws<SqlSyntaxException.DuplicateObject>(builder.ToDefinition);

        Assert.Equal(24, exception.ObjectSourceRef?.SourceId);
    }

    [Theory]
    [InlineData("CREATE FUNCTION audit_fn() RETURNS INT RETURN 1")]
    [InlineData("CREATE FUNCTION audit_fn(value INT) RETURNS trigger LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION audit_fn() RETURNS SETOF trigger LANGUAGE plpgsql AS 'opaque'")]
    [InlineData("CREATE FUNCTION audit_fn() RETURNS void LANGUAGE plpgsql AS 'opaque'")]
    public void ToDefinition_RejectsInvalidTriggerFunctionSignatures(string function)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"""
            CREATE TABLE items (id INT);

            {function};

            CREATE TRIGGER audit
            AFTER INSERT ON items
            EXECUTE FUNCTION audit_fn()
            """), GetSchema(), 25);

        Assert.Throws<SqlSyntaxException.InvalidTrigger>(builder.ToDefinition);
    }

    [Fact]
    public void ToDefinition_ReportsMissingTriggerFunctionWithLocation()
    {
        const string sql = "CREATE TABLE items (id INT); CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION missing()";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 26);

        var exception = Assert.Throws<SqlSyntaxException.TriggerFunctionNotFound>(builder.ToDefinition);

        Assert.Equal("missing", exception.Function.ToSql());
        Assert.Equal(26, exception.ObjectSourceRef?.SourceId);
        Assert.Equal(sql.IndexOf("missing", StringComparison.Ordinal), exception.ObjectSourceRef?.Start);
    }

    [Theory]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON missing EXECUTE FUNCTION audit_fn()")]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER INSERT ON items FROM missing FOR ROW EXECUTE FUNCTION audit_fn()")]
    public void ToDefinition_ReportsMissingTriggerRelationsWithLocation(string trigger)
    {
        string sql = "CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; " + trigger;
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 27);

        var exception = Assert.Throws<SqlSyntaxException.TriggerRelationNotFound>(builder.ToDefinition);

        Assert.Equal("missing", exception.Relation.ToSql());
        Assert.Equal(27, exception.ObjectSourceRef?.SourceId);
        Assert.Equal(sql.IndexOf("missing", StringComparison.Ordinal), exception.ObjectSourceRef?.Start);
    }

    [Fact]
    public void ToDefinition_TriggersFollowConfiguredSearchPathAndQuotedCase()
    {
        var config = PgTestConfig.GetTestConfig(searchPath: [new Identifier("app"), new Identifier("public")]);
        var builder = new PgDefinitionBuilder(config, new SourceManager(), NullLogger<PgDefinitionBuilder>.Instance);
        builder.ProcessStatements(ParseStatements("CREATE TABLE public.items (other INT); CREATE TABLE app.items (id INT); CREATE FUNCTION public.audit_fn() RETURNS INT RETURN 1; CREATE FUNCTION app.audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER \"Audit\" AFTER UPDATE OF id ON items EXECUTE FUNCTION audit_fn()"), GetSchema(), 0);

        PgTrigger trigger = Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values);

        Assert.Equal("Audit", trigger.Name.Name);
        Assert.Equal("app", trigger.Name.Schema.Name);
        Assert.Equal("app", trigger.OnTable.Schema.Name);
        Assert.Equal("app", trigger.FunctionName.Schema.Name);
    }

    [Fact]
    public void ToDefinition_ConstraintTriggerResolvesDefaultDeferrability()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE CONSTRAINT TRIGGER immediate AFTER UPDATE ON items FOR EACH ROW EXECUTE FUNCTION audit_fn(); CREATE CONSTRAINT TRIGGER deferred AFTER UPDATE ON items INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION audit_fn()"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        DatabaseComponentDict<PgTrigger> triggers = Assert.Single(definition.Triggers.Values);
        Assert.Equal(new PgTriggerConstraintOptions(null, false, false), triggers[new Identifier("immediate")].ConstraintOptions);
        Assert.Equal(new PgTriggerConstraintOptions(null, true, true), triggers[new Identifier("deferred")].ConstraintOptions);
    }

    [Fact]
    public void ToDefinition_TriggerArgumentQuotingIsLosslessAndSafe()
    {
        ObjectIdentifier table = ObjectIdentifier.FromStrings("catalog", "public", "items");
        string[] arguments = ["", "''", "x'); DROP TABLE items; --", "a\\'b", "MixedCase"];
        var original = new PgTrigger(ObjectIdentifier.FromStrings("catalog", "public", "audit"), table,
            TriggerTime.After, [new TriggerEvent.Insert()], ObjectIdentifier.FromStrings("catalog", "public", "audit_fn"), [.. arguments]);
        var builder = CreateBuilder();
        var statements = ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; " + original.ToCreateStatement(true).ToSql());

        builder.ProcessStatements(statements, GetSchema(), 0);

        Assert.Equal(3, statements.Count);
        Assert.Equal(arguments, Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values).Arguments);
    }

    [Theory]
    [InlineData("empty events")]
    [InlineData("empty UPDATE OF")]
    [InlineData("empty REFERENCING")]
    [InlineData("OR REPLACE CONSTRAINT")]
    [InlineData("OR ALTER")]
    [InlineData("wrong body")]
    [InlineData("qualified name")]
    [InlineData("IF NOT EXISTS")]
    [InlineData("long argument")]
    [InlineData("boolean argument")]
    [InlineData("unknown event")]
    [InlineData("unknown quantifier")]
    [InlineData("unsupported WHEN")]
    [InlineData("function without argument list")]
    public void ToDefinition_ValidatesManuallyConstructedTriggerSyntax(string scenario)
    {
        var statements = ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit_fn()");
        var original = Assert.IsType<CreateTrigger>(statements[^1]);
        var execute = Assert.IsType<PgTriggerExecute>(original.Body);
        CreateTrigger trigger = scenario switch
        {
            "empty events" => original with { Events = [] },
            "empty UPDATE OF" => original with { Events = [new TriggerEvent.Update([])] },
            "empty REFERENCING" => original with { PgOptions = new PgStatementTriggerOptions { TransitionRelations = [] } },
            "OR REPLACE CONSTRAINT" => original with
            {
                PgOptions = new PgStatementTriggerOptions { ConstraintOptions = new PgTriggerConstraint(null, null, null) },
                CreateOrLabel = CreateOrLabel.OrReplace,
                ExecutionQuantifier = new TriggerExecutionQuantifier.ForEach(TriggerForEachType.Row)
            },
            "OR ALTER" => original with { CreateOrLabel = CreateOrLabel.OrAlter },
            "wrong body" => original with { Body = new Return(new LiteralValue(new Value.Number("1", false))) },
            "qualified name" => original with { Name = new ObjectName([new Identifier("public"), new Identifier("audit")]) },
            "IF NOT EXISTS" => original with { IfNotExists = true },
            "long argument" => original with { Body = execute with { Arguments = [new PgStatementTriggerArgument.Literal(new Value.Number("1", true))] } },
            "boolean argument" => original with { Body = execute with { Arguments = [new PgStatementTriggerArgument.Literal(new Value.Boolean(true))] } },
            "unknown event" => original with { Events = [new UnknownTriggerEvent()] },
            "unknown quantifier" => original with { ExecutionQuantifier = new UnknownTriggerQuantifier() },
            "unsupported WHEN" => original with { PgOptions = new PgStatementTriggerOptions { When = new Wildcard() } },
            "function without argument list" => original with
            {
                PgOptions = new PgStatementTriggerOptions
                {
                    When = new FunctionCall(new ObjectName(new Identifier("allowed"))) { Arguments = new FunctionArguments.None() }
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        statements[^1] = trigger;
        var builder = CreateBuilder();
        builder.ProcessStatements(statements, GetSchema(), 28);

        var exception = Assert.Throws<SqlSyntaxException.InvalidTrigger>(builder.ToDefinition);

        Assert.Equal(28, exception.ObjectSourceRef?.SourceId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ToDefinition_MissingManualTriggerReferencesUseStatementSource(bool missingFunction, bool qualified)
    {
        var statements = ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit_fn()");
        var original = Assert.IsType<CreateTrigger>(statements[^1]);
        var missing = new ObjectName(qualified ? [new Identifier("public"), new Identifier("missing")] : [new Identifier("missing")]);
        statements[^1] = missingFunction
            ? original with { Body = Assert.IsType<PgTriggerExecute>(original.Body) with { FunctionName = missing } }
            : original with { TableName = missing };
        var builder = CreateBuilder();
        builder.ProcessStatements(statements, GetSchema(), 29);

        var exception = Assert.ThrowsAny<SourcedException>(builder.ToDefinition);

        Assert.Equal(missingFunction ? typeof(SqlSyntaxException.TriggerFunctionNotFound) : typeof(SqlSyntaxException.TriggerRelationNotFound), exception.GetType());
        Assert.Equal(new SourceRef(29, original.Meta.Start!.Value, original.Meta.End!.Value), exception.ObjectSourceRef);
    }

    [Theory]
    [InlineData("$arg$it's unchanged$arg$", "it's unchanged")]
    [InlineData("E'it\\'s\\n\\u00e9'", "it's\n\u00e9")]
    [InlineData("U&'it''s!000a!00e9' UESCAPE '!'", "it's\n\u00e9")]
    [InlineData("U&\"it\\0027s\"", "it's")]
    public void ToDefinition_ResolvesQuotedTriggerArguments(string literal, string expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION audit_fn({literal})"), GetSchema(), 0);

        PgTrigger trigger = Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values);

        Assert.Equal([expected], trigger.Arguments);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION audit_fn() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; " + trigger.ToCreateStatement(includeSchema: true).ToSql()), GetSchema(), 0);
        Assert.Equal(trigger.Arguments, Assert.Single(Assert.Single(rebuilt.ToDefinition().Triggers.Values).Values).Arguments);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void TriggerModel_ReportsRelationAndFunctionDependenciesWithoutPseudoRows(bool hasFrom, bool includeReferences, bool hasWhen)
    {
        ObjectIdentifier table = ObjectIdentifier.FromStrings("catalog", "public", "items");
        var trigger = new PgTrigger(ObjectIdentifier.FromStrings("catalog", "public", "audit"), table,
            TriggerTime.After, [new TriggerEvent.Update()], ObjectIdentifier.FromStrings("catalog", "public", "audit_fn"), [])
        {
            ForEachType = TriggerForEachType.Row,
            ConstraintOptions = hasFrom ? new PgTriggerConstraintOptions(ObjectIdentifier.FromStrings("catalog", "public", "parent"), false, false) : null,
            When = hasWhen ? ParseExpression("util.allowed(NEW.id)") : null
        };
        var context = new ReferencedItemsManager { Filters = includeReferences ? ObjectNameFilters.All : ObjectNameFilters.None };

        var references = trigger.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().ToArray();

        string[] expected = includeReferences
            ? hasFrom ? ["public.items", "public.parent", "public.audit_fn"] : ["public.items", "public.audit_fn"]
            : [];
        Assert.Equal(hasWhen && includeReferences ? [.. expected, "util.allowed"] : expected, references.Select(reference => reference.Name.ToSql()));
        Assert.Equal(expected, trigger.ToCreateStatement(true).GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql()));
        if (hasFrom)
        {
            Assert.Equal("parent", trigger.ToCreateStatement(false).PgOptions!.ConstraintOptions!.FromTable!.ToSql());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessStatements_RejectsIncompleteStatementMetadata(bool hasStart)
    {
        Statement statement = Assert.Single(ParseStatements("CREATE TABLE items (id INT)"));
        statement = statement with { Meta = new MetaData { Start = hasStart ? 0 : null, End = hasStart ? null : 27 } };
        var builder = CreateBuilder();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.ProcessStatements([statement], GetSchema(), 0));

        Assert.Contains("missing source position metadata", exception.Message);
    }

    [Theory]
    [InlineData("CREATE TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE DOMAIN mood AS INT")]
    public void ToDefinition_RejectsIndexOnCustomType(string declaration)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(declaration + "; CREATE INDEX ix ON mood (id)"), GetSchema(), 30);

        var exception = Assert.Throws<SqlSyntaxException.CreateIndexUnknownTable>(builder.ToDefinition);

        Assert.Equal(30, exception.ObjectSourceRef?.SourceId);
    }

    [Fact]
    public void ToDefinition_ResolvesCommentsAfterTheirTargetsAcrossFiles()
    {
        var builder = CreateBuilder();
        string body1 = """
        COMMENT ON TABLE items IS 'table';
        COMMENT ON COLUMN items.id IS 'column';
        COMMENT ON FUNCTION f(IN arg INTEGER, OUT result TEXT) IS 'function';
        COMMENT ON PROCEDURE p() IS 'procedure';
        COMMENT ON INDEX ix IS 'index';
        COMMENT ON TYPE mood IS 'type';
        COMMENT ON DOMAIN label IS 'domain';
        COMMENT ON TRIGGER audit ON items IS 'trigger'
        """;
        builder.ProcessStatements(ParseStatements(body1), GetSchema(), 31);
        string body2 = """
        CREATE TABLE items (id INT);
        CREATE INDEX ix ON items (id);
        CREATE TYPE mood AS ENUM ('happy');
        CREATE DOMAIN label AS TEXT;
        CREATE FUNCTION f(IN arg INT, OUT result TEXT) LANGUAGE SQL AS 'opaque';
        CREATE PROCEDURE p() LANGUAGE SQL AS 'opaque';
        CREATE FUNCTION fire() RETURNS trigger LANGUAGE plpgsql AS 'opaque';
        CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION fire()
        """;
        builder.ProcessStatements(ParseStatements(body2), GetSchema(), 32);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(8, definition.Comments.Count);
        KeyValuePair<PgCommentTarget, string> column = Assert.Single(definition.Comments, comment => comment.Key is PgCommentTarget.Column);
        Assert.Equal("column", column.Value);
        Assert.Equal("COMMENT ON COLUMN \"public\".\"items\".\"id\" IS 'column'", new CommentOn(column.Key.ToStatementTarget(), new Value.SingleQuotedString(column.Value)).ToSql());
        Assert.Equal(definition.Comments, builder.ToDefinition().Comments);
    }

    [Theory]
    [InlineData("COMMENT ON TABLE items IS 'one'", "COMMENT ON TABLE public.items IS 'two'")]
    [InlineData("COMMENT ON COLUMN items.id IS 'one'", "COMMENT ON COLUMN public.items.id IS 'one'")]
    [InlineData("COMMENT ON TABLE items IS NULL", "COMMENT ON TABLE items IS 'two'")]
    [InlineData("COMMENT ON TABLE items IS NULL", "COMMENT ON TABLE public.items IS NULL")]
    [InlineData("COMMENT ON TABLE items IS ''", "COMMENT ON TABLE public.items IS NULL")]
    [InlineData("COMMENT ON TABLE items IS ''", "COMMENT ON TABLE items IS 'two'")]
    [InlineData("COMMENT ON FUNCTION f IS 'one'", "COMMENT ON FUNCTION public.f(INT) IS 'two'")]
    [InlineData("COMMENT ON DOMAIN label IS 'one'", "COMMENT ON TYPE label IS 'two'")]
    public void ToDefinition_RejectsDuplicateResolvedCommentTargets(string first, string second)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION f(arg INT) RETURNS INT RETURN arg; CREATE DOMAIN label AS TEXT; " + first), GetSchema(), 33);
        builder.ProcessStatements(ParseStatements(second), GetSchema(), 34);

        var exception = Assert.Throws<SqlSyntaxException.DuplicateObject>(builder.ToDefinition);

        Assert.Equal(34, exception.ObjectSourceRef?.SourceId);
    }

    [Theory]
    [InlineData("COMMENT ON TABLE absent IS NULL")]
    [InlineData("COMMENT ON TABLE absent IS ''")]
    [InlineData("COMMENT ON COLUMN items.absent IS 'missing'")]
    [InlineData("COMMENT ON COLUMN absent.id IS 'missing'")]
    [InlineData("COMMENT ON FUNCTION absent() IS 'missing'")]
    [InlineData("COMMENT ON FUNCTION f(TEXT) IS 'wrong signature'")]
    [InlineData("COMMENT ON PROCEDURE f(INT) IS 'wrong kind'")]
    [InlineData("COMMENT ON TYPE absent IS 'missing'")]
    [InlineData("COMMENT ON DOMAIN items IS 'wrong kind'")]
    [InlineData("COMMENT ON INDEX absent IS 'missing'")]
    [InlineData("COMMENT ON TRIGGER absent ON items IS 'missing'")]
    public void ToDefinition_RejectsMissingCommentTargetsWithSource(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE FUNCTION f(arg INT) RETURNS INT RETURN arg;"), GetSchema(), 35);
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 36);

        var exception = Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(builder.ToDefinition);

        Assert.Equal(36, exception.ObjectSourceRef?.SourceId);
    }

    [Fact]
    public void ToDefinition_NullAndEmptyCommentsAreAbsentButWhitespaceIsPreserved()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("COMMENT ON TABLE items IS NULL; COMMENT ON COLUMN items.id IS ''; COMMENT ON COLUMN items.label IS ' '; CREATE TABLE items (id INT, label TEXT)"), GetSchema(), 37);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(" ", Assert.Single(definition.Comments).Value);
        Assert.IsType<PgCommentTarget.Column>(Assert.Single(definition.Comments).Key);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("it\\'s\\n\\xC3\\xA9", "it's\n\u00e9")]
    [InlineData("it\\'; DROP TABLE items; --", "it'; DROP TABLE items; --")]
    public void ToDefinition_ResolvesEscapeStringComments(string raw, string expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (id INT); COMMENT ON TABLE items IS E'{raw}'"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        Assert.Single(definition.Tables.Values);
        if (expected.Length == 0)
        {
            Assert.Empty(definition.Comments);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(definition.Comments).Value);
        }
    }

    [Theory]
    [InlineData("U&''", "")]
    [InlineData("U&'it''s!000a!00e9' UESCAPE '!'", "it's\n\u00e9")]
    [InlineData("U&'it\\0027; DROP TABLE items; --'", "it'; DROP TABLE items; --")]
    public void ToDefinition_ResolvesUnicodeStringCommentsAndIdentifierTargets(string literal, string expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (id INT); COMMENT ON TABLE U&\"it\\0065ms\" IS {literal}"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        Assert.Single(definition.Tables.Values);
        if (expected.Length == 0)
        {
            Assert.Empty(definition.Comments);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(definition.Comments).Value);
            var comment = new CommentOn(Assert.Single(definition.Comments).Key.ToStatementTarget(), new Value.SingleQuotedString(expected.Replace("'", "''", StringComparison.Ordinal)));
            var rebuilt = CreateBuilder();
            rebuilt.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); " + comment.ToSql()), GetSchema(), 0);
            Assert.Equal(expected, Assert.Single(rebuilt.ToDefinition().Comments).Value);
        }
    }

    [Fact]
    public void ToDefinition_CommentsFollowSearchPathAndPreserveQuotedCase()
    {
        var config = PgTestConfig.GetTestConfig(searchPath: [new Identifier("app"), new Identifier("public")]);
        var builder = new PgDefinitionBuilder(config, new SourceManager(), NullLogger<PgDefinitionBuilder>.Instance);
        builder.ProcessStatements(ParseStatements("COMMENT ON TABLE items IS 'app'; COMMENT ON TABLE public.items IS 'public'; COMMENT ON COLUMN items.ID IS 'lower'; COMMENT ON COLUMN items.\"ID\" IS 'upper'; CREATE TABLE public.items (other INT); CREATE TABLE app.items (id INT, \"ID\" INT)"), GetSchema(), 38);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(4, definition.Comments.Count);
        Assert.Equal("app", Assert.IsType<PgCommentTarget.DatabaseObject>(Assert.Single(definition.Comments, comment => comment.Value == "app").Key).Name.Schema.Name);
        Assert.Equal("id", Assert.IsType<PgCommentTarget.Column>(Assert.Single(definition.Comments, comment => comment.Value == "lower").Key).Name.Name);
        Assert.Equal("ID", Assert.IsType<PgCommentTarget.Column>(Assert.Single(definition.Comments, comment => comment.Value == "upper").Key).Name.Name);
    }

    [Theory]
    [InlineData("VARCHAR(20)", "VARCHAR")]
    [InlineData("NUMERIC(10,2)", "DECIMAL")]
    [InlineData("CHAR(10)", "BPCHAR")]
    [InlineData("BIT(8)", "BIT")]
    [InlineData("BIT VARYING(8)", "BIT VARYING")]
    [InlineData("TIME", "TIME WITHOUT TIME ZONE")]
    [InlineData("TIMESTAMP WITH TIME ZONE", "TIMESTAMP WITH TIME ZONE")]
    [InlineData("TIMESTAMP(3)", "TIMESTAMP WITHOUT TIME ZONE")]
    [InlineData("TIME(3) WITH TIME ZONE", "TIME WITH TIME ZONE")]
    [InlineData("INTERVAL DAY TO SECOND(3)", "INTERVAL")]
    [InlineData("INT[][]", "INT[]")]
    [InlineData("label[]", "public.label[]")]
    public void ToDefinition_CommentRoutineSignaturesUseInputTypeIdentity(string declared, string requested)
    {
        string statements = $"""
        COMMENT ON FUNCTION f(IN ignored {requested}, OUT ignored TEXT) IS 'routine';
        CREATE DOMAIN label AS INT;
        CREATE FUNCTION f(IN value {declared}, OUT result TEXT)
        LANGUAGE SQL AS 'opaque'
        """;
        var builder = CreateBuilder();

        builder.ProcessStatements(ParseStatements(statements), GetSchema(), 39);

        KeyValuePair<PgCommentTarget, string> comment = Assert.Single(builder.ToDefinition().Comments);

        var routine = Assert.IsType<PgCommentTarget.Routine>(comment.Key);
        Assert.Single(routine.ArgumentTypes);
        var roundTripBuilder = CreateBuilder();
        roundTripBuilder.ProcessStatements(ParseStatements($"CREATE DOMAIN label AS INT; CREATE FUNCTION f(IN value {declared}, OUT result TEXT) LANGUAGE SQL AS 'opaque'; " + new CommentOn(routine.ToStatementTarget(), new Value.SingleQuotedString(comment.Value)).ToSql()), GetSchema(), 40);
        Assert.Equal(builder.ToDefinition().Comments, roundTripBuilder.ToDefinition().Comments);
    }

    [Fact]
    public void ToDefinition_CommentsOnSameNamedTriggersOnDifferentTablesAreDistinct()
    {
        string statements = """
        COMMENT ON TRIGGER audit ON first IS 'first';
        COMMENT ON TRIGGER audit ON second IS 'second';

        CREATE TABLE first (id INT);
        CREATE TABLE second (id INT);

        CREATE FUNCTION fire()
            RETURNS trigger LANGUAGE plpgsql AS 'opaque';
        CREATE TRIGGER audit
            AFTER INSERT ON first
            EXECUTE FUNCTION fire();
        CREATE TRIGGER audit
            AFTER INSERT ON second
            EXECUTE FUNCTION fire()
        """;
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(statements), GetSchema(), 41);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(2, definition.Comments.Count);
        Assert.All(definition.Comments, comment => Assert.Equal(comment.Value, Assert.IsType<PgCommentTarget.Trigger>(comment.Key).Table.Name));
    }

    [Theory]
    [InlineData("VIEW")]
    [InlineData("SEQUENCE")]
    public void ToDefinition_UnsupportedCommentTargetsAreDiagnosed(string kind)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"COMMENT ON {kind} object_name IS 'text'"), GetSchema(), 42);

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);

        Assert.Equal(42, exception.ObjectSourceRef?.SourceId);
    }

    [Theory]
    [InlineData("'it''s'", "it's")]
    [InlineData("'it''''s'", "it''s")]
    [InlineData("$tag$it''s; opaque$tag$", "it''s; opaque")]
    [InlineData("$$NULL$$", "NULL")]
    [InlineData("$$text$$", "text")]
    public void ToDefinition_DecodesCommentLiteralValues(string literal, string expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"COMMENT ON TABLE items IS {literal}; CREATE TABLE items (id INT)"), GetSchema(), 43);

        PgDefinition definition = builder.ToDefinition();

        Assert.Equal(expected, Assert.Single(definition.Comments).Value);
    }

    [Theory]
    [InlineData("number")]
    [InlineData("boolean")]
    [InlineData("national string")]
    [InlineData("hex string")]
    public void ToDefinition_RejectsInvalidManualCommentValues(string scenario)
    {
        Value value = scenario switch
        {
            "number" => new Value.Number("1", false),
            "boolean" => new Value.Boolean(true),
            "national string" => new Value.NationalStringLiteral("text"),
            "hex string" => new Value.HexString("1234"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var comment = Assert.IsType<CommentOn>(ParseStatement("COMMENT ON TABLE items IS NULL")) with { Comment = value };
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT)"), GetSchema(), 44);
        builder.ProcessStatements([comment], GetSchema(), 45);

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);

        Assert.Equal(45, exception.ObjectSourceRef?.SourceId);
    }

    [Fact]
    public void ToDefinition_ResolvesCommentsOnTableRowTypesAndIndexesPastOtherEntries()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE INDEX first_ix ON items (id); CREATE INDEX second_ix ON items (id); COMMENT ON TYPE items IS 'row type'; COMMENT ON INDEX second_ix IS 'index'"), GetSchema(), 46);

        PgDefinition definition = builder.ToDefinition();

        var type = Assert.IsType<PgCommentTarget.DatabaseObject>(Assert.Single(definition.Comments, comment => comment.Value == "row type").Key);
        Assert.Equal(PgCommentObjectType.Type, type.ObjectType);
        Assert.Equal("items", type.Name.Name);
        var index = Assert.IsType<PgCommentTarget.DatabaseObject>(Assert.Single(definition.Comments, comment => comment.Value == "index").Key);
        Assert.Equal(PgCommentObjectType.Index, index.ObjectType);
        Assert.Equal("second_ix", index.Name.Name);
    }

    [Theory]
    [InlineData("COMMENT ON TABLE absent IS 'missing'")]
    [InlineData("COMMENT ON COLUMN id IS 'unqualified'")]
    [InlineData("COMMENT ON INDEX missing_ix IS 'missing'")]
    [InlineData("COMMENT ON DOMAIN mood IS 'not a domain'")]
    [InlineData("COMMENT ON TRIGGER absent ON items IS 'missing'")]
    public void ToDefinition_RejectsUnresolvedCommentTargetsAfterSearchingExistingObjects(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT); CREATE INDEX ix ON items (id); CREATE TYPE mood AS ENUM ('happy'); CREATE FUNCTION fire() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit AFTER INSERT ON items EXECUTE FUNCTION fire()"), GetSchema(), 47);
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 48);

        var exception = Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(builder.ToDefinition);

        Assert.Equal(48, exception.ObjectSourceRef?.SourceId);
    }

    [Fact]
    public void CommentTargets_EmitCanonicalTriggerTargets()
    {
        var target = new PgCommentTarget.Trigger(ObjectIdentifier.FromStrings("catalog", "app", "items", QuoteStyle.Ansi),
            new Identifier("Audit", QuoteStyle.Ansi));

        var statementTarget = Assert.IsType<PgStatementCommentTarget.Trigger>(target.ToStatementTarget());

        Assert.Equal("TRIGGER \"Audit\" ON \"app\".\"items\"", statementTarget.ToSql());
        Assert.Equal("COMMENT ON TRIGGER \"Audit\" ON \"app\".\"items\" IS 'trigger'",
            new CommentOn(statementTarget, new Value.SingleQuotedString("trigger")).ToSql());
    }

    [Fact]
    public void ToDefinition_RejectsUnknownManualCommentTargetWithoutInventingReferences()
    {
        var parsed = Assert.IsType<CommentOn>(ParseStatement("COMMENT ON TABLE items IS NULL"));
        var comment = parsed with { Target = new UnknownCommentTarget() };
        var builder = CreateBuilder();
        builder.ProcessStatements([comment], GetSchema(), 50);

        Assert.Empty(comment.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(builder.ToDefinition);

        Assert.Equal(50, exception.ObjectSourceRef?.SourceId);
    }

    [Fact]
    public void ToDefinition_CommentSignaturesFlattenNestedManualArrayReferences()
    {
        var parsed = Assert.IsType<CommentOn>(ParseStatement("COMMENT ON FUNCTION f(INT[]) IS 'array routine'"));
        var target = Assert.IsType<PgStatementCommentTarget.Routine>(parsed.Target);
        var parameter = Assert.IsType<StatementRoutineParameter.General>(Assert.Single(target.Parameters!));
        var arrayReference = Assert.IsType<DataTypeReference.Array>(parameter.DataType);
        var nested = new DataTypeReference.Array(arrayReference, arrayReference.Dimensions);
        var comment = parsed with { Target = target with { Parameters = [parameter with { DataType = nested }] } };
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE FUNCTION f(value INT[]) RETURNS INT LANGUAGE SQL AS 'opaque'"), GetSchema(), 51);
        builder.ProcessStatements([comment], GetSchema(), 52);

        var routine = Assert.IsType<PgCommentTarget.Routine>(Assert.Single(builder.ToDefinition().Comments).Key);

        var array = Assert.IsType<PgDataType.Array>(Assert.Single(routine.ArgumentTypes));
        Assert.IsType<DataType.Int>(array.ElementType);
        Assert.Equal("FUNCTION \"public\".\"f\"(INT[])", routine.ToStatementTarget().ToSql());
    }

    [Theory]
    [InlineData("B'00101'")]
    [InlineData("X'0aF'")]
    [InlineData("B''")]
    [InlineData("X''")]
    public void BitStrings_TableDefaultsSurviveCanonicalDefinitionRoundTrips(string literal)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (flags BIT VARYING(16) DEFAULT {literal})"), GetSchema(), 0);
        var table = Assert.Single(builder.ToDefinition().Tables.Values);
        string canonical = table.ToCreateStatement(includeSchema: true).ToSql();

        Assert.Contains("DEFAULT " + literal, canonical);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(canonical), GetSchema(), 0);
        Assert.Equal(canonical, Assert.Single(rebuilt.ToDefinition().Tables.Values).ToCreateStatement(includeSchema: true).ToSql());
    }

    [Theory]
    [InlineData("0x_Ff_01", "65281")]
    [InlineData("0o7_5_5", "493")]
    [InlineData("0b1_0_1", "5")]
    [InlineData("1_000.2_5", "1000.25")]
    public void ExtendedNumbers_TableDefaultsSurviveCanonicalDefinitionRoundTrips(string literal, string expectedDecimalText)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (amount NUMERIC DEFAULT {literal})"), GetSchema(), 0);
        var table = Assert.Single(builder.ToDefinition().Tables.Values);
        string canonical = table.ToCreateStatement(includeSchema: true).ToSql();

        decimal expected = decimal.Parse(expectedDecimalText, CultureInfo.InvariantCulture);
        var defaultValue = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(Assert.Single(table.Columns).Default);
        var number = Assert.IsType<Value.Number>(defaultValue.Value);
        Assert.Equal(expected, decimal.Parse(number.DecimalText, NumberStyles.Float, CultureInfo.InvariantCulture));

        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(canonical), GetSchema(), 0);
        var rebuiltTable = Assert.Single(rebuilt.ToDefinition().Tables.Values);
        var rebuiltDefault = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(Assert.Single(rebuiltTable.Columns).Default);
        var rebuiltNumber = Assert.IsType<Value.Number>(rebuiltDefault.Value);
        Assert.Equal(expected, decimal.Parse(rebuiltNumber.DecimalText, NumberStyles.Float, CultureInfo.InvariantCulture));
        Assert.Equal(canonical, rebuiltTable.ToCreateStatement(includeSchema: true).ToSql());
    }

    [Theory]
    [InlineData("0x10", "0b10", 16f, 2f)]
    [InlineData("2_5.0", "0o7", 25f, 7f)]
    public void ExtendedNumbers_RoutineEstimatesResolveNumericMeaning(string cost, string rows, float expectedCost, float expectedRows)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE FUNCTION f() RETURNS SETOF INT LANGUAGE SQL COST {cost} ROWS {rows} AS 'opaque'"), GetSchema(), 0);
        var function = Assert.Single(builder.ToDefinition().Functions).Value;

        Assert.NotNull(function.Options);
        Assert.Equal(expectedCost, function.Options.Cost);
        Assert.Equal(expectedRows, function.Options.Rows);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(function.ToCreateStatement(true).ToSql()), GetSchema(), 0);
        Assert.Equal(function, Assert.Single(rebuilt.ToDefinition().Functions).Value);
    }

    [Fact]
    public void ExtendedNumbers_IdentityClausesResolveRadixAndSeparators()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE items (id INT GENERATED ALWAYS AS IDENTITY (START WITH 0x10 INCREMENT BY 0b10 CACHE 1_0))"), GetSchema(), 0);
        var table = Assert.Single(builder.ToDefinition().Tables.Values);
        string canonical = table.ToCreateStatement(includeSchema: true).ToSql();

        Assert.Contains("START WITH 16", canonical);
        Assert.Contains("INCREMENT BY 2", canonical);
        Assert.Contains("CACHE 10", canonical);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements(canonical), GetSchema(), 0);
        Assert.Equal(canonical, Assert.Single(rebuilt.ToDefinition().Tables.Values).ToCreateStatement(includeSchema: true).ToSql());
    }

    [Theory]
    [InlineData("0x10", "16")]
    [InlineData("0o20", "16")]
    [InlineData("0b1_0000", "16")]
    [InlineData("1_6", "16")]
    [InlineData("0xFFFFFFFF", "0xFFFFFFFF")]
    public void ExtendedNumbers_TriggerArgumentsFollowIntegerVersusNumericTextRules(string literal, string expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"CREATE TABLE items (id INT); CREATE FUNCTION fire() RETURNS trigger LANGUAGE plpgsql AS 'opaque'; CREATE TRIGGER audit BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION fire({literal})"), GetSchema(), 0);
        var trigger = Assert.Single(Assert.Single(builder.ToDefinition().Triggers.Values).Values);

        Assert.Equal(expected, Assert.Single(trigger.Arguments));
    }

    private static SchemaIdentifier GetSchema()
    {
        return new SchemaIdentifier("public", new CatalogIdentifier("catalog"));
    }

    private static PgDefinitionBuilder CreateBuilder(SourceManager? sourceManager = null)
    {
        return new PgDefinitionBuilder(PgTestConfig.GetTestConfig(), sourceManager ?? new SourceManager(), NullLogger<PgDefinitionBuilder>.Instance);
    }

    private sealed record UnknownCommentTarget : PgStatementCommentTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            throw new NotSupportedException();
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record UnknownTriggerEvent : TriggerEvent
    {
        public override void ToSql(SqlTextWriter writer)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record UnknownTriggerQuantifier : TriggerExecutionQuantifier
    {
        public override void ToSql(SqlTextWriter writer)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record UnknownFunctionReturn : StatementFunctionReturn
    {
        public override void ToSql(SqlTextWriter writer)
        {
            throw new NotSupportedException();
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NameInspectingBuilder(PgConfig config)
        : PgDefinitionBuilder(config, new SourceManager(), NullLogger<PgDefinitionBuilder>.Instance)
    {
        public ObjectNameAndHandle GetName(CreateTable table, SchemaIdentifier schema)
        {
            return GetAndValidateName(table, schema, null);
        }
    }
}
