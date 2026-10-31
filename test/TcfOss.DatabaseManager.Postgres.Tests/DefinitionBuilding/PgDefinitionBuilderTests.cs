using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
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
    public void ToDefinition_RejectsCyclicTypeDependencies(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 9);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToDefinition());

        Assert.Contains("Cyclic PostgreSQL type dependency", exception.Message);
        Assert.Contains(" -> ", exception.Message);
        Assert.Equal(9, Assert.IsType<SourceRef>(exception.Data[nameof(SourceRef)]).SourceId);
    }

    [Theory]
    [InlineData("CREATE DOMAIN label AS missing")]
    [InlineData("CREATE TYPE details AS (label missing)")]
    [InlineData("CREATE TABLE items (label missing)")]
    public void ToDefinition_RejectsMissingTypesWithSource(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 8);

        var exception = Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => builder.ToDefinition());

        Assert.Contains("missing", exception.Message);
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
        IDatabaseObject<CreateTable> tableContract = table;
        CreateTable tableStatement = tableContract.ToCreateStatement(includeSchema, null!);
        Assert.Equal(ObjectType.Table, tableContract.ObjectType);
        Assert.Equal(includeSchema ? 2 : 1, tableStatement.Name.Values.Count);
        var statements = new List<Statement> { tableStatement };
        foreach (PgCustomType type in original.CustomTypes.Values)
        {
            IDatabaseObject<Statement> typeContract = type;
            Statement statement = typeContract.ToCreateStatement(includeSchema, null!);
            Assert.Equal(ObjectType.Type, typeContract.ObjectType);
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
    public void ToDefinition_RejectsMissingRoutineSignatureTypes(string sql)
    {
        var builder = CreateBuilder();
        builder.ProcessStatements(ParseStatements(sql), GetSchema(), 0);

        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => builder.ToDefinition());
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
        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => builder.ToDefinition());
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

        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToDefinition());

        Assert.DoesNotContain("blocked", exception.Message);
        Assert.Contains("first", exception.Message);
        Assert.Contains("second", exception.Message);
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

    private static SchemaIdentifier GetSchema()
    {
        return new SchemaIdentifier("public", new CatalogIdentifier("catalog"));
    }

    private static PgDefinitionBuilder CreateBuilder(SourceManager? sourceManager = null)
    {
        var config = new PgConfig
        {
            ProjectDirectory = ".",
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            QuoteStyle = QuoteStyle.Ansi,
            NameHandling = NameHandling.LowercaseUnlessQuoted,
        };
        return new PgDefinitionBuilder(config, sourceManager ?? new SourceManager(), NullLogger<PgDefinitionBuilder>.Instance);
    }
}
