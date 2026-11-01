using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
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

    [Fact]
    public void ProcessStatements_RejectsUnsupportedStatements()
    {
        var builder = CreateBuilder();

        Assert.Throws<DefinitionException>(() => builder.ProcessStatements(ParseStatements("SELECT 1"), GetSchema(), 0));
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

        var exception = Assert.Throws<SqlSyntaxException.CyclicTypeDependency>(() => builder.ToDefinition());

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
    public void ToDefinition_RejectsMissingTypesWithSource(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 8);

        var exception = Assert.Throws<SqlSyntaxException.TypeNotFound>(() => builder.ToDefinition());

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
        builder.ProcessStatements(ParseStatements("CREATE FUNCTION Echo(value label) RETURNS label RETURN value; CREATE PROCEDURE save(IN value label) SELECT value; CREATE DOMAIN label AS TEXT"), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        PgStoredFunction function = Assert.Single(definition.Functions).Value;
        PgStoredProcedure procedure = Assert.Single(definition.Procedures).Value;
        Assert.Equal("echo", function.Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(function.ReturnType).Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.Single(function.Parameters).DataType).Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.Single(procedure.Parameters).DataType).Name.Name);
        Assert.NotNull(Assert.IsType<RoutineParameter.Directed>(Assert.Single(procedure.Parameters)).Direction);
        Assert.IsType<PgCustomType.Domain>(Assert.Single(definition.CustomTypes).Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Routines_UseCoreContractsAndRoundTripResolvedSignatures(bool includeSchema)
    {
        const string types = "CREATE DOMAIN app.label AS TEXT; CREATE TYPE app.mood AS ENUM ('happy')";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements($"{types}; CREATE FUNCTION Echo(value app.label) RETURNS app.mood RETURN 'happy'; CREATE PROCEDURE save(IN value app.label) SELECT value"), GetSchema(), 0);
        PgDefinition original = builder.ToDefinition();
        StoredFunction function = Assert.Single(original.Functions).Value;
        StoredProcedure procedure = Assert.Single(original.Procedures).Value;

        CreateFunction functionStatement = function.ToCreateStatement(includeSchema);
        CreateProcedure procedureStatement = procedure.ToCreateStatement(includeSchema);

        Assert.Equal(includeSchema ? 2 : 1, functionStatement.Name.Values.Count);
        Assert.Equal(includeSchema ? 2 : 1, procedureStatement.Name.Values.Count);
        Assert.Equal(ObjectType.Function, function.ObjectType);
        Assert.Equal(ObjectType.Procedure, procedure.ObjectType);
        var rebuilt = CreateBuilder();
        rebuilt.ProcessStatements(ParseStatements($"{types}; {functionStatement.ToSql()}; {procedureStatement.ToSql()}"), GetSchema(), 1);
        PgDefinition result = rebuilt.ToDefinition();
        StoredFunction rebuiltFunction = Assert.Single(result.Functions).Value;
        StoredProcedure rebuiltProcedure = Assert.Single(result.Procedures).Value;
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
    [InlineData("CREATE OR REPLACE FUNCTION Echo() RETURNS INT AS RETURN 1; CREATE OR REPLACE PROCEDURE save() AS SELECT 1")]
    [InlineData("CREATE FUNCTION IF NOT EXISTS Echo() RETURNS INT AS RETURN 1; CREATE PROCEDURE IF NOT EXISTS save() AS SELECT 1")]
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
        plainBuilder.ProcessStatements(ParseStatements("CREATE FUNCTION Echo() RETURNS INT RETURN 1; CREATE PROCEDURE save() SELECT 1"), GetSchema(), 0);
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
    [InlineData("CREATE PROCEDURE save(value missing) SELECT value")]
    [InlineData("CREATE FUNCTION echo(value missing[]) RETURNS TEXT RETURN value")]
    [InlineData("CREATE FUNCTION echo(value TEXT) RETURNS missing[] RETURN value")]
    [InlineData("CREATE PROCEDURE save(value missing[]) SELECT value")]
    public void ToDefinition_RejectsMissingRoutineSignatureTypes(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.TypeNotFound>(() => builder.ToDefinition());
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

        Assert.ThrowsAny<SqlSyntaxException>(() => builder.ToDefinition());
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
                new Core.Statements.Components.CompositeTypeField(new Identifier(name, quoteStyle), new DataType.Text()),
                new Core.Statements.Components.CompositeTypeField(new Identifier(name, quoteStyle), new DataType.Int())
            ]
        };
        builder.ProcessStatements([statement], GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.DuplicateColumn>(() => builder.ToDefinition());
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

        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() => builder.ToDefinition());
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
        Assert.Throws<SqlSyntaxException.TypeNotFound>(() => builder.ToDefinition());
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

        var exception = Assert.Throws<SqlSyntaxException.CyclicTypeDependency>(() => builder.ToDefinition());

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

        var exception = Assert.Throws<SqlSyntaxException.CyclicTypeDependency>(() => builder.ToDefinition());

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

        Assert.Throws<ValidationExceptionSet>(() => builder.ToDefinition());
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
                new Core.Statements.Components.StatementDomainConstraint.Nullability(first),
                new Core.Statements.Components.StatementDomainConstraint.Nullability(second)
            ]
        };
        builder.ProcessStatements([statement], GetSchema(), 0);

        Assert.Throws<ValidationExceptionSet>(() => builder.ToDefinition());
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

        Assert.Throws<NotSupportedException>(() => builder.ProcessStatements(ParseStatements("CREATE TEMP TABLE items (id INT)"), GetSchema(), 0));
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
            + "CREATE PROCEDURE save(IN values mood[]) SELECT values";
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        PgDefinition definition = builder.ToDefinition();

        var domain = Assert.IsType<PgCustomType.Domain>(definition.CustomTypes[new ObjectHandle("catalog", "public", "labels")]);
        Assert.IsType<DataType.Text>(Assert.IsType<PgDataType.Array>(domain.BaseType).ElementType);
        PgTable table = Assert.Single(definition.Tables).Value;
        Assert.Equal("labels", Assert.IsType<PgDataType.Custom>(table.Columns[1].DataType).Name.Name);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(Assert.IsType<PgDataType.Array>(table.Columns[2].DataType).ElementType).Name.Name);
        PgStoredFunction function = Assert.Single(definition.Functions).Value;
        Assert.Equal("labels", Assert.IsType<PgDataType.Custom>(Assert.IsType<PgDataType.Array>(function.ReturnType).ElementType).Name.Name);
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

        Assert.Throws<SqlSyntaxException.IndexColumnNotFound>(() => builder.ToDefinition());
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

        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => builder.ToDefinition());
    }

    [Theory]
    [InlineData("CREATE TABLE parent (id INT); CREATE TABLE child (id INT, CONSTRAINT fk_parent FOREIGN KEY (id) REFERENCES parent(id))")]
    [InlineData("CREATE TABLE parent (id INT); CREATE UNIQUE INDEX uq ON parent (id) WHERE id > 0; CREATE TABLE child (id INT, CONSTRAINT fk_parent FOREIGN KEY (id) REFERENCES parent(id))")]
    public void ForeignKeys_RejectMissingOrPartialBackingKeys(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        var exception = Assert.Throws<ValidationExceptionSet>(() => builder.ToDefinition());
        Assert.Contains(exception.Exceptions, error => error is DefinitionException.ForeignKeyNoReferencedBackingIndex);
    }

    [Theory]
    [InlineData("CREATE TABLE parent (id INT PRIMARY KEY); CREATE TABLE child (id INT, CONSTRAINT fk_parent FOREIGN KEY (id) REFERENCES parent(missing))", typeof(SqlSyntaxException.ForeignKeyReferencedColumnNotFound))]
    [InlineData("CREATE TABLE parent (id INT PRIMARY KEY); CREATE TABLE child (first INT, second INT, CONSTRAINT fk_parent FOREIGN KEY (first, second) REFERENCES parent(id))", typeof(SqlSyntaxException.ForeignKeyColumnCountMismatch))]
    public void ForeignKeys_ValidateReferencedColumnsAndCounts(string sql, Type expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        var exception = Assert.Throws<ValidationExceptionSet>(() => builder.ToDefinition());
        Assert.Contains(exception.Exceptions, error => error.GetType() == expected);
    }

    [Fact]
    public void ForeignKeys_RejectMissingReferencedTables()
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements("CREATE TABLE child (id INT, CONSTRAINT fk_missing FOREIGN KEY (id) REFERENCES missing(id))"), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.ForeignKeyReferencedTableNotFound>(() => builder.ToDefinition());
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

        Assert.Throws<SqlSyntaxException.DuplicateObject>(() => builder.ToDefinition());
    }

    [Theory]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON missing (id)", typeof(SqlSyntaxException.CreateIndexUnknownTable))]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items (missing)", typeof(SqlSyntaxException.IndexColumnNotFound))]
    [InlineData("CREATE TABLE items (id INT); CREATE INDEX ix ON items USING HASH (id DESC)", typeof(SqlSyntaxException.IndexDirectionNotSupported))]
    public void Indexes_RejectInvalidTablesColumnsAndOrdering(string sql, Type expected)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Exception exception = Assert.ThrowsAny<SqlSyntaxException>(() => builder.ToDefinition());
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

        var exception = Assert.Throws<NotSupportedException>(() => builder.ToDefinition());
        Assert.Contains("HASH indexes do not support INCLUDE", exception.Message);
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
            var exception = Assert.Throws<ValidationExceptionSet>(() => builder.ToDefinition());
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

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.ToDefinition());
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
        var builder = new NameInspectingBuilder(CreateConfig());

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
        var builder = new NameInspectingBuilder(CreateConfig());
        var statements = ParseStatements("CREATE TABLE Items (id INT); CREATE TABLE \"Items\" (id INT)");

        ObjectNameAndHandle unquoted = builder.GetName(Assert.IsType<CreateTable>(statements[0]), GetSchema());
        ObjectNameAndHandle quoted = builder.GetName(Assert.IsType<CreateTable>(statements[1]), GetSchema());

        Assert.Equal("items", unquoted.Id.Name);
        Assert.Equal("Items", quoted.Id.Name);
        Assert.NotEqual(unquoted.Key, quoted.Key);
    }

    private static SchemaIdentifier GetSchema()
    {
        return new SchemaIdentifier("public", new CatalogIdentifier("catalog"));
    }

    private static PgDefinitionBuilder CreateBuilder(SourceManager? sourceManager = null)
    {
        return new PgDefinitionBuilder(CreateConfig(), sourceManager ?? new SourceManager(), NullLogger<PgDefinitionBuilder>.Instance);
    }

    private static PgConfig CreateConfig()
    {
        return new PgConfig
        {
            ProjectDirectory = ".",
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            QuoteStyle = QuoteStyle.Ansi,
            NameHandling = NameHandling.LowercaseUnlessQuoted,
        };
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
