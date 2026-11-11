using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Postgres.DefinitionBuilding;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.DefinitionBuilding;

public class PgTableBuilderTests : ParserTestsBase<PgLexer, PgParser>
{
    [Fact]
    public void ApplyCreateTable_NormalizesColumnsAndRetainsPendingTypes()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (Id INT, \"Mood\" app.mood DEFAULT 'happy')"));
        var builder = CreateBuilder();

        builder.ApplyCreateTable(statement);

        Assert.Equal(2, builder.Columns.Count);
        Assert.Equal("Mood", builder.Columns[1].Name.Name);
        Assert.IsType<DataTypeReference.Named>(builder.Columns[1].DataType);
        Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(builder.Columns[1].Default);
        Assert.Equal("id", builder.Columns[0].Name.Name);
        Assert.Same(builder.ToPseudoTable().Identifier, builder.Columns[0].Name.Table);
        Assert.False(builder.Columns[1].NotNull);
        PseudoTable pseudoTable = builder.ToPseudoTable();
        Assert.Equal(new[] { "id", "Mood" }, pseudoTable.SelectableItems);
        Assert.Equal(PseudoTableType.Table, pseudoTable.Type);
        Assert.Throws<NotImplementedException>(builder.ToTable);
    }

    [Theory]
    [InlineData("Id INT, id INT")]
    [InlineData("Id INT, \"id\" INT")]
    public void ApplyCreateTable_RejectsDuplicateColumns(string columns)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items ({columns})"));
        var builder = CreateBuilder();

        Assert.Throws<SqlSyntaxException.DuplicateColumn>(() => builder.ApplyCreateTable(statement));
    }

    [Fact]
    public void ApplyCreateTable_PreservesDistinctQuotedColumnNames()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (Id INT, \"Id\" INT)"));
        var builder = CreateBuilder();

        builder.ApplyCreateTable(statement);

        Assert.Equal(new[] { "id", "Id" }, builder.ToPseudoTable().SelectableItems);
    }

    [Fact]
    public void ApplyCreateTable_RejectsUnimplementedConstraints()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT, KEY ix (id))"));
        var builder = CreateBuilder();

        Assert.Throws<NotImplementedException>(() => builder.ApplyCreateTable(statement));
    }

    [Fact]
    public void ApplyCreateTable_RejectsUnimplementedOptions()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT)"));
        statement = statement with { TableOptions = [new TestTableOption()] };
        var builder = CreateBuilder();

        Assert.Throws<NotImplementedException>(() => builder.ApplyCreateTable(statement));
    }

    [Fact]
    public void ToTable_ResolvesColumnsAndPreservesDefaultsAndNullability()
    {
        ObjectIdentifier tableName = ObjectIdentifier.FromStrings("catalog", "public", "items");
        ObjectIdentifier typeName = ObjectIdentifier.FromStrings("catalog", "public", "label");
        var config = PgTestConfig.GetTestConfig();
        var manager = new PgTypeResolutionManager(config, new Dictionary<ObjectHandle, ObjectIdentifier>
        {
            [new ObjectHandle("catalog", "public", "items")] = tableName,
            [new ObjectHandle("catalog", "public", "label")] = typeName
        });
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (Id INT CONSTRAINT Required NOT NULL CONSTRAINT DefaultId DEFAULT 1, \"Label\" label NULL CONSTRAINT \"DefaultLabel\" DEFAULT 'x', plain TEXT, nullable TEXT DEFAULT NULL, calculated INT DEFAULT (1 + 2))"));
        var literal = new Value.Number("7", false);
        statement.Columns.Add(new StatementColumn.Typed(new Identifier("literal"), new DataType.Int(),
        [
            new StatementColumnOption.Default.DefaultExpression(new LiteralValue(literal), new Identifier("LiteralDefault"))
        ]));
        var builder = CreateBuilder();
        builder.ApplyCreateTable(statement);

        var table = builder.ToTable(manager);

        Assert.Equal("id", table.Columns[0].Name.Name);
        var nullability = Assert.IsType<ColumnOption.Nullability.NotNull>(table.Columns[0].Nullability);
        Assert.Equal("required", nullability.Name?.Name);
        Assert.IsType<DataType.Int>(table.Columns[0].DataType);
        var defaultId = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(table.Columns[0].Default);
        Assert.Equal("1", defaultId.Value.ToSql());
        Assert.Equal("defaultid", defaultId.Name?.Name);

        Assert.Equal("Label", table.Columns[1].Name.Name);
        Assert.IsType<ColumnOption.Nullability.Null>(table.Columns[1].Nullability);
        Assert.Equal("label", Assert.IsType<PgDataType.Custom>(table.Columns[1].DataType).Name.Name);
        var defaultLabel = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(table.Columns[1].Default);
        Assert.Equal("'x'", defaultLabel.Value.ToSql());
        Assert.Equal("DefaultLabel", defaultLabel.Name?.Name);

        Assert.Null(table.Columns[2].Nullability);
        Assert.Null(table.Columns[2].Default);

        Assert.IsType<Value.Null>(Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(table.Columns[3].Default).Value);

        var expressionDefault = Assert.IsType<ColumnOption.ColumnDefault.DefaultExpression>(table.Columns[4].Default);
        Expression parsedExpression = Assert.IsType<StatementColumnOption.Default.DefaultExpression>(
            Assert.Single(Assert.IsType<StatementColumn.Typed>(statement.Columns[4]).Options)).Expression;
        Assert.Same(parsedExpression, expressionDefault.Expression);
        Assert.Same(parsedExpression, expressionDefault.NormalizedExpression);

        var literalDefault = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(table.Columns[5].Default);
        Assert.Same(literal, literalDefault.Value);
        Assert.Equal("literaldefault", literalDefault.Name?.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToTable_NormalizesLiteralDefaultsThroughInjectedComponentNormalizer(bool asExpression)
    {
        ObjectIdentifier tableName = ObjectIdentifier.FromStrings("catalog", "public", "items");
        var expected = new Value.Number("99", false);
        var normalizer = new DefaultValueNormalizer(expected);
        var config = PgTestConfig.GetTestConfig();
        var manager = new PgTypeResolutionManager(config, new Dictionary<ObjectHandle, ObjectIdentifier>
        {
            [new ObjectHandle("catalog", "public", "items")] = tableName
        }, normalizer);
        var literal = new Value.Number("1", false);
        var constraintName = new Identifier("DefaultName");
        StatementColumnOption.Default option = asExpression
            ? new StatementColumnOption.Default.DefaultExpression(new LiteralValue(literal), constraintName)
            : new StatementColumnOption.Default.DefaultValue(literal, constraintName);
        var builder = new PgTableBuilder(tableName, config.NameHandling, normalizer, null);
        builder.AddColumn(new StatementColumn.Typed(new Identifier("value"), new DataType.Int(), [option]), null);
        var pendingDefault = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(Assert.Single(builder.Columns).Default);
        Assert.Same(expected, pendingDefault.Value);
        Assert.Equal("defaultname", pendingDefault.Name?.Name);

        var table = builder.ToTable(manager);

        var result = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(Assert.Single(table.Columns).Default);
        Assert.Same(pendingDefault, result);
        Assert.Same(expected, result.Value);
        Assert.Equal("defaultname", result.Name?.Name);
    }

    [Fact]
    public void AddColumn_NormalizesNullabilityDefaultsAndBuiltInArraysImmediately()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (ID INT[3][4] CONSTRAINT Required NOT NULL CONSTRAINT DefaultId DEFAULT NULL, custom app.mood[])"));
        var builder = CreateBuilder();

        builder.ApplyCreateTable(statement);

        PgTableBuilder.PreColumn column = builder.Columns[0];
        Assert.Equal("id", column.Name.Name);
        Assert.Equal("required", Assert.IsType<ColumnOption.Nullability.NotNull>(column.Nullability).Name!.Name);
        var defaultValue = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(column.Default);
        Assert.Equal("defaultid", defaultValue.Name!.Name);
        Assert.IsType<Value.Null>(defaultValue.Value);
        Assert.True(column.NotNull);
        Assert.IsType<DataType.Int>(Assert.IsType<PgDataType.Array>(Assert.IsType<DataTypeReference.BuiltIn>(column.DataType).DataType).ElementType);
        Assert.IsType<DataTypeReference.Named>(Assert.IsType<DataTypeReference.Array>(builder.Columns[1].DataType).ElementType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddColumn_RejectsDuplicateOptionsImmediately(bool defaults)
    {
        var builder = CreateBuilder();
        var source = new SourceRef(7, 0, 10);
        var column = new StatementColumn.Typed(new Identifier("id"), new DataType.Int(),
            [new StatementColumnOption.Nullability.Null(), new StatementColumnOption.Nullability.NotNull()]);
        if (defaults)
        {
            column = column with
            {
                Options = [new StatementColumnOption.Default.DefaultValue(new Value.Null()),
                    new StatementColumnOption.Default.DefaultValue(new Value.Null())]
            };
        }

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() => builder.AddColumn(column, source));
        Assert.Equal(source, exception.ObjectSourceRef);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrimaryKeys_SetPendingColumnNotNullInEitherDeclarationOrder(bool keyFirst)
    {
        var builder = CreateBuilder();
        var primaryKey = new StatementTableConstraint.PrimaryKey([new KeyPart.Column(new Identifier("ID"))]);
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(
            Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (Id INT NULL)")).Columns));
        if (keyFirst)
        {
            builder.AddIndexOrConstraint(primaryKey, null);
        }
        builder.AddColumn(column, null);
        if (!keyFirst)
        {
            Assert.False(Assert.Single(builder.Columns).NotNull);
            builder.AddIndexOrConstraint(primaryKey, null);
        }

        Assert.True(Assert.Single(builder.Columns).NotNull);
        Assert.IsType<ColumnOption.Nullability.NotNull>(Assert.Single(builder.Columns).Nullability);
        var table = builder.ToTable(CreateManager());
        Assert.Same(builder.ToPseudoTable().Identifier, table.Name);
        Assert.Same(Assert.Single(builder.Columns).Name, Assert.Single(table.Columns).Name);
        Assert.IsType<ColumnOption.Nullability.NotNull>(Assert.Single(table.Columns).Nullability);
    }

    [Fact]
    public void UniqueConstraints_NormalizeAtAddAndDeferColumnExistence()
    {
        var builder = CreateBuilder();
        var tableStatement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT, CONSTRAINT UQ UNIQUE (ID) INCLUDE (Payload))"));
        var unique = Assert.IsType<StatementTableConstraint.UniqueConstraint>(Assert.Single(tableStatement.Constraints));

        builder.AddIndexOrConstraint(unique, null);
        unique.Columns.Clear();
        unique.IncludedColumns!.Columns.Clear();
        Assert.Throws<SqlSyntaxException.IndexColumnNotFound>(() => builder.ToTable(CreateManager()));
        builder.AddColumn(new StatementColumn.Typed(new Identifier("Id"), new DataType.Int(), []), null);
        Assert.Throws<SqlSyntaxException.IndexColumnNotFound>(() => builder.ToTable(CreateManager()));
        builder.AddColumn(new StatementColumn.Typed(new Identifier("Payload"), new DataType.Text(), []), null);

        var result = Assert.Single(builder.ToTable(CreateManager()).UniqueConstraints.Values);
        Assert.Equal("uq", result.Name!.Name);
        Assert.Equal("id", Assert.Single(result.Columns).Name);
        Assert.Equal("payload", Assert.Single(result.IncludedColumns!.Columns).Name);
    }

    [Fact]
    public void Indexes_NormalizeAtAddAndDeferColumnExistence()
    {
        var builder = CreateBuilder();
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX IX ON items (ID DESC) INCLUDE (Payload)"));

        builder.AddIndex(index, null);
        index.Columns.Clear();
        index.IncludedColumns!.Columns.Clear();
        Assert.Throws<SqlSyntaxException.IndexColumnNotFound>(() => builder.ToTable(CreateManager()));
        builder.AddColumn(new StatementColumn.Typed(new Identifier("Id"), new DataType.Int(), []), null);
        Assert.Throws<SqlSyntaxException.IndexColumnNotFound>(() => builder.ToTable(CreateManager()));
        builder.AddColumn(new StatementColumn.Typed(new Identifier("Payload"), new DataType.Text(), []), null);

        var result = Assert.Single(builder.ToTable(CreateManager()).Indexes.Values);
        Assert.Equal("ix", result.Name.Name);
        var part = Assert.IsType<KeyPart.Column>(Assert.Single(result.Columns));
        Assert.Equal("id", part.Name.Name);
        Assert.NotNull(part.Direction);
        Assert.Equal("payload", Assert.Single(result.IncludedColumns!.Columns).Name);
    }

    [Fact]
    public void AddIndex_RejectsEmptyIncludeWithSourcedSyntaxError()
    {
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items (id) INCLUDE (payload)")) with
        {
            IncludedColumns = new IncludedColumns([])
        };
        var sourceRef = new SourceRef(4, 10, 20);
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.EmptyIndexColumnList>(() => builder.AddIndex(index, sourceRef));

        Assert.Contains("INCLUDE", exception.Message);
        Assert.Equal(sourceRef, exception.ObjectSourceRef);
    }

    [Fact]
    public void AddPrimaryKey_RejectsEmptyColumnListWithSourcedSyntaxError()
    {
        var table = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT, PRIMARY KEY (id))"));
        var constraint = Assert.IsType<StatementTableConstraint.PrimaryKey>(Assert.Single(table.Constraints)) with
        {
            Columns = []
        };
        var sourceRef = new SourceRef(4, 10, 20);
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.EmptyIndexColumnList>(() => builder.AddIndexOrConstraint(constraint, sourceRef));

        Assert.Contains("primary key", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sourceRef, exception.ObjectSourceRef);
    }

    [Theory]
    [InlineData("CREATE INDEX ix ON items (id DESC)")]
    [InlineData("CREATE INDEX ix ON items ((id + 1))")]
    public void AddPrimaryKey_RejectsNonColumnKeyPartsWithSourcedSyntaxError(string sql)
    {
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement(sql));
        var constraint = new StatementTableConstraint.PrimaryKey(index.Columns);
        var sourceRef = new SourceRef(4, 10, 20);
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.AddIndexOrConstraint(constraint, sourceRef));

        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Contains("PRIMARY KEY/UNIQUE constraints", exception.Clause);
        Assert.Equal(sourceRef, exception.ObjectSourceRef);
    }

    [Fact]
    public void AddIndex_RejectsEmptyKeyListWithSourcedSyntaxError()
    {
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items (id)")) with
        {
            Columns = []
        };
        var sourceRef = new SourceRef(4, 10, 20);
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.EmptyIndexColumnList>(() => builder.AddIndex(index, sourceRef));

        Assert.Contains("index", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sourceRef, exception.ObjectSourceRef);
    }

    [Fact]
    public void AddIndex_RejectsPrefixLengthWithSourcedSyntaxError()
    {
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items (id)")) with
        {
            Columns = [new KeyPart.Column(new Identifier("id"), 3)]
        };
        var sourceRef = new SourceRef(4, 10, 20);
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.AddIndex(index, sourceRef));

        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Equal("index column prefix lengths", exception.Clause);
        Assert.Equal(sourceRef, exception.ObjectSourceRef);
    }

    [Fact]
    public void AddIndex_RejectsUniqueHashIndexWithSourcedSyntaxError()
    {
        var index = Assert.IsType<CreateIndex.Unique>(ParseStatement("CREATE UNIQUE INDEX ix ON items USING HASH (id)"));
        var sourceRef = new SourceRef(4, 10, 20);
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.AddIndex(index, sourceRef));

        Assert.Equal("PostgreSQL", exception.Dialect);
        Assert.Equal("UNIQUE USING HASH", exception.Clause);
        Assert.Equal(sourceRef, exception.ObjectSourceRef);
    }

    [Fact]
    public void CheckExpressions_RejectExternalScopesAtAddTime()
    {
        var builder = CreateBuilder();
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT, CONSTRAINT ck CHECK (other.id > 0))"));

        Assert.Throws<NotImplementedException>(() => builder.AddIndexOrConstraint(Assert.Single(statement.Constraints), null));
    }

    [Fact]
    public void IndexExpressions_RejectExternalScopesAtAddTime()
    {
        var builder = CreateBuilder();
        var statement = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items ((other.id + 1))"));

        Assert.Throws<NotImplementedException>(() => builder.AddIndex(statement, null));
    }

    [Theory]
    [InlineData("SMALLINT", "", true, 1L, 1L, 1L, 32767L)]
    [InlineData("INT", "", false, 1L, 1L, 1L, 2147483647L)]
    [InlineData("BIGINT", "", true, 1L, 1L, 1L, long.MaxValue)]
    [InlineData("SMALLINT", "(INCREMENT -1)", false, -1L, -1L, -32768L, -1L)]
    [InlineData("INT", "(INCREMENT BY -1)", true, -1L, -1L, -2147483648L, -1L)]
    [InlineData("BIGINT", "(INCREMENT -1)", false, -1L, -1L, long.MinValue, -1L)]
    public void IdentityOptions_ResolveDefaultsAfterIntegerTypeResolution(string type, string options, bool always,
        long start, long increment, long minimum, long maximum)
    {
        string mode = always ? "ALWAYS" : "BY DEFAULT";
        var statement = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id {type} GENERATED {mode} AS IDENTITY {options})"));
        var builder = CreateBuilder();
        builder.ApplyCreateTable(statement);
        PgTableBuilder.PreColumn pending = Assert.Single(builder.Columns);
        Assert.NotNull(pending.Identity);
        Assert.Null(pending.Generated);
        Assert.True(pending.NotNull);

        var table = builder.ToTable(CreateManager());
        var column = Assert.Single(table.Columns);
        var identity = Assert.IsType<ColumnOption.Generated.PgIdentity>(column.Generated);
        Assert.Equal(always, identity.Always);
        Assert.Equal(new PgIdentityOptions(start, increment, minimum, maximum, 1, false), identity.Options);
        Assert.IsType<ColumnOption.Nullability.NotNull>(column.Nullability);
        Assert.Equal(6, identity.Options.ToStatementOptions().Count);

        var rebuilt = CreateBuilder();
        rebuilt.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items ({column.ToStatementColumn().ToSql()})")));
        Assert.Equal(identity, Assert.Single(rebuilt.ToTable(CreateManager()).Columns).Generated);
    }

    [Theory]
    [InlineData("MINVALUE 10 MAXVALUE 20", 10L, 1L, 10L, 20L, 1L, false)]
    [InlineData("MAXVALUE -5 MINVALUE -20 INCREMENT -3 CACHE 4 CYCLE START -8", -8L, -3L, -20L, -5L, 4L, true)]
    [InlineData("INCREMENT -2 NO MINVALUE NO MAXVALUE NO CYCLE", -1L, -2L, long.MinValue, -1L, 1L, false)]
    [InlineData("CACHE 2 START WITH 12 MINVALUE 10 MAXVALUE 20 INCREMENT BY 2", 12L, 2L, 10L, 20L, 2L, false)]
    public void IdentityOptions_NormalizeExplicitBoundsAndNoForms(string options, long start, long increment,
        long minimum, long maximum, long cache, bool cycle)
    {
        var builder = CreateBuilder();
        builder.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id BIGINT GENERATED ALWAYS AS IDENTITY ({options}))")));
        var identity = Assert.IsType<ColumnOption.Generated.PgIdentity>(Assert.Single(builder.ToTable(CreateManager()).Columns).Generated);

        Assert.Equal(new PgIdentityOptions(start, increment, minimum, maximum, cache, cycle), identity.Options);
    }

    [Theory]
    [InlineData("CACHE 3 START 7 INCREMENT 2 NO MINVALUE MAXVALUE 20 CYCLE")]
    [InlineData("NO CYCLE NO MAXVALUE MINVALUE -20 INCREMENT BY -2 START WITH -5 CACHE 4")]
    public void IdentityStatements_PreserveOptionOrderAndSpelling(string options)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id BIGINT GENERATED BY DEFAULT AS IDENTITY ({options}))"));
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(statement.Columns));
        var identity = Assert.IsType<StatementColumnOption.Generated.PgIdentity>(Assert.Single(column.Options));

        Assert.NotNull(identity.Options);
        Assert.Equal(options, identity.Options.ToSqlDelimited(" "));
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("START 1 START WITH 2")]
    [InlineData("INCREMENT 1 INCREMENT BY 2")]
    [InlineData("MINVALUE 1 NO MINVALUE")]
    [InlineData("NO MAXVALUE MAXVALUE 20")]
    [InlineData("CACHE 1 CACHE 2")]
    [InlineData("CYCLE NO CYCLE")]
    public void IdentityStatements_RejectDuplicateCategories(string options)
    {
        Assert.Throws<ParseException.Duplicate>(() => ParseStatement($"CREATE TABLE items (id INT GENERATED ALWAYS AS IDENTITY ({options}))"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NO START 1")]
    [InlineData("NO CACHE 1")]
    [InlineData("START")]
    [InlineData("UNKNOWN 1")]
    public void IdentityStatements_RejectMalformedOptions(string options)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"CREATE TABLE items (id INT GENERATED ALWAYS AS IDENTITY ({options}))"));
    }

    [Theory]
    [InlineData("INT", "INCREMENT 0")]
    [InlineData("INT", "CACHE 0")]
    [InlineData("INT", "CACHE -1")]
    [InlineData("SMALLINT", "MINVALUE -32769")]
    [InlineData("SMALLINT", "MAXVALUE 32768")]
    [InlineData("SMALLINT", "MINVALUE 32768")]
    [InlineData("SMALLINT", "MAXVALUE -32769")]
    [InlineData("INT", "MINVALUE 10 MAXVALUE 10")]
    [InlineData("INT", "MINVALUE 20 MAXVALUE 10")]
    [InlineData("INT", "START 0")]
    [InlineData("INT", "START 21 MINVALUE 10 MAXVALUE 20")]
    public void IdentityOptions_RejectInvalidEffectiveSequenceValues(string type, string options)
    {
        var builder = CreateBuilder();
        builder.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id {type} GENERATED ALWAYS AS IDENTITY ({options}))")));

        Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.ToTable(CreateManager()));
    }

    [Fact]
    public void IdentityOptions_RejectNonIntegerTypesAndManualDuplicates()
    {
        var builder = CreateBuilder();
        builder.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id TEXT GENERATED ALWAYS AS IDENTITY)")));
        Assert.Throws<SqlSyntaxException.AutoIncrementIntegerOnly>(() => builder.ToTable(CreateManager()));

        var duplicateBuilder = CreateBuilder();
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT GENERATED ALWAYS AS IDENTITY (CACHE 1))"));
        var identity = Assert.IsType<StatementColumnOption.Generated.PgIdentity>(Assert.Single(Assert.IsType<StatementColumn.Typed>(Assert.Single(statement.Columns)).Options));
        Assert.NotNull(identity.Options);
        identity.Options.Add(new PgStatementSequenceOption.Cache(2));
        duplicateBuilder.ApplyCreateTable(statement);
        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() => duplicateBuilder.ToTable(CreateManager()));
    }

    [Theory]
    [InlineData("DEFAULT 1 GENERATED ALWAYS AS IDENTITY", "IDENTITY", "DEFAULT")]
    [InlineData("GENERATED ALWAYS AS IDENTITY DEFAULT 1", "IDENTITY", "DEFAULT")]
    [InlineData("NULL GENERATED ALWAYS AS IDENTITY", "IDENTITY", "NULL")]
    [InlineData("GENERATED ALWAYS AS IDENTITY NULL", "IDENTITY", "NULL")]
    [InlineData("DEFAULT 1 GENERATED ALWAYS AS (1) STORED", "GENERATED", "DEFAULT")]
    [InlineData("GENERATED ALWAYS AS (1) STORED DEFAULT 1", "GENERATED", "DEFAULT")]
    public void GeneratedColumns_RejectConflictingDefaultAndNullability(string options, string attribute1, string attribute2)
    {
        var source = new SourceRef(7, 0, 10);
        var builder = CreateBuilder(source);
        var statement = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT {options})"));
        var column = Assert.Single(statement.Columns);

        var exception = Assert.Throws<SqlSyntaxException.MutuallyExclusive>(() => builder.AddColumn(column, null));

        Assert.Contains($"The attributes '{attribute1}' and '{attribute2}' are mutually exclusive on column id.", exception.Message);
        Assert.Equal(source, exception.ObjectSourceRef);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("VIRTUAL", false)]
    [InlineData("STORED", true)]
    public void GeneratedExpressions_PreserveEffectiveStorageAndRoundTrip(string storage, bool stored)
    {
        var builder = CreateBuilder();
        builder.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT, calculated INT GENERATED ALWAYS AS (id + 1) {storage})")));
        var column = builder.ToTable(CreateManager()).Columns[1];
        var generated = Assert.IsType<ColumnOption.Generated.AsExpression>(column.Generated);

        Assert.Equal(stored ? GenerationMode.Stored : GenerationMode.Virtual, generated.Mode);
        Assert.Contains(stored ? "STORED" : "VIRTUAL", column.ToStatementColumn().ToSql());
        var rebuilt = CreateBuilder();
        rebuilt.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT, {column.ToStatementColumn().ToSql()})")));
        Assert.Equal(generated, rebuilt.ToTable(CreateManager()).Columns[1].Generated);
    }

    [Theory]
    [InlineData("AS (1)")]
    [InlineData("IDENTITY")]
    [InlineData("GENERATED ALWAYS AS (1) PERSISTENT")]
    [InlineData("GENERATED BY DEFAULT AS (1)")]
    public void GeneratedColumns_RejectNonPostgresForms(string options)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"CREATE TABLE items (id INT {options})"));
    }

    [Theory]
    [InlineData("ALWAYS")]
    [InlineData("BY DEFAULT")]
    public void IdentityStatements_WithoutOptions_RoundTrip(string mode)
    {
        string sql = $"CREATE TABLE items (id INT GENERATED {mode} AS IDENTITY)";
        var statement = Assert.IsType<CreateTable>(ParseStatement(sql));
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(statement.Columns));
        var identity = Assert.IsType<StatementColumnOption.Generated.PgIdentity>(Assert.Single(column.Options));

        Assert.Null(identity.Options);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("PRIMARY KEY (id)")]
    [InlineData("CONSTRAINT pk PRIMARY KEY (id)")]
    public void PrimaryKeys_AllowIndexesWithDistinctNames(string constraint)
    {
        var builder = CreateBuilder();
        builder.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT, {constraint})")));
        builder.AddIndex(Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items (id)")), null);

        var table = builder.ToTable(CreateManager());

        Assert.NotNull(table.PrimaryKey);
        Assert.Equal("ix", Assert.Single(table.Indexes.Values).Name.Name);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("items.id")]
    [InlineData("public.items.id")]
    [InlineData("catalog.public.items.id")]
    public void CheckExpressions_AcceptOwnColumnReferencesAtEveryQualificationLevel(string reference)
    {
        var builder = CreateBuilder(new SourceRef(7, 0, 10));
        builder.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT, CONSTRAINT ck CHECK ({reference} > 0))")));

        Assert.Single(builder.ToTable(CreateManager()).Checks.Values);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("(id + 1)")]
    public void HashIndexes_AcceptKeyPartsWithoutSortDirections(string columns)
    {
        var builder = CreateBuilder();
        builder.ApplyCreateTable(Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT)")));
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement($"CREATE INDEX ix ON items USING HASH ({columns})")) with { Options = [] };
        builder.AddIndex(index, null);

        Assert.Equal((BuiltInIndexMethod)BuiltInIndexMethod.Hash, Assert.Single(builder.ToTable(CreateManager()).Indexes.Values).Method);
    }

    [Fact]
    public void AddColumn_RejectsComputedDeclarationsAndUnsupportedOptions()
    {
        var builder = CreateBuilder();
        var expression = new LiteralValue(new Value.Number("1", false));

        Assert.Throws<NotImplementedException>(() => builder.AddColumn(new StatementColumn.MsComputed(new Identifier("computed"), expression, []), null));
        Assert.Throws<NotImplementedException>(() => builder.AddColumn(new StatementColumn.Typed(new Identifier("id"), new DataType.Int(), [new StatementColumnOption.AutoIncrement()]), null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddIndex_RejectsMissingNamesWithTableSourceFallback(bool unique)
    {
        var source = new SourceRef(7, 0, 10);
        var builder = CreateBuilder(source);
        CreateIndex index = unique
            ? Assert.IsType<CreateIndex.Unique>(ParseStatement("CREATE UNIQUE INDEX ix ON items (id)"))
            : Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items (id)"));
        index = index with { Name = null };

        var error = Assert.Throws<DefinitionException.TableConstraintNameRequired>(() => builder.AddIndex(index, null));

        Assert.Equal(source, error.ObjectSourceRef);
        Assert.Contains("INDEX", error.Message);
    }

    [Fact]
    public void AddIndex_RejectsUnsupportedIndexVariants()
    {
        var index = new CreateIndex.FullText([new KeyPart.Column(new Identifier("id"))], new Identifier("ix"), new ObjectName(new Identifier("items")));

        Assert.Throws<NotSupportedException>(() => CreateBuilder().AddIndex(index, null));
    }

    [Theory]
    [InlineData("CONSTRAINT ix PRIMARY KEY (id)", false)]
    [InlineData("CONSTRAINT ix PRIMARY KEY (id)", true)]
    [InlineData("CONSTRAINT ix UNIQUE (id)", false)]
    [InlineData("CONSTRAINT ix UNIQUE (id)", true)]
    public void IndexAndConstraintNames_RejectCollisionsInBothDeclarationOrders(string constraintSql, bool indexFirst)
    {
        var source = new SourceRef(7, 0, 10);
        var builder = CreateBuilder(source);
        var table = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT, {constraintSql})"));
        builder.AddColumn(Assert.Single(table.Columns), null);
        StatementTableConstraint constraint = Assert.Single(table.Constraints);
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items (id)"));

        void addConstraint()
        {
            builder.AddIndexOrConstraint(constraint, null);
        }

        void addIndex()
        {
            builder.AddIndex(index, null);
        }

        if (indexFirst)
        {
            addIndex();
        }
        else
        {
            addConstraint();
        }

        var error = Assert.Throws<SqlSyntaxException.DuplicateObject>(indexFirst ? addConstraint : addIndex);

        Assert.Contains("ix", error.Message);
        Assert.Equal(source, error.ObjectSourceRef);
    }

    [Theory]
    [InlineData("id DESC")]
    [InlineData("(id + 1) DESC")]
    public void HashIndexes_RejectSortDirections(string columns)
    {
        var source = new SourceRef(7, 0, 10);
        var builder = CreateBuilder(source);
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement($"CREATE INDEX ix ON items USING HASH ({columns})"));

        var error = Assert.Throws<SqlSyntaxException.IndexDirectionNotSupported>(() => builder.AddIndex(index, null));

        Assert.Contains("HASH", error.Message);
        Assert.Equal(source, error.ObjectSourceRef);
    }

    [Fact]
    public void HashIndexes_RejectUnknownPublicKeyPartVariants()
    {
        var source = new SourceRef(7, 0, 10);
        var builder = CreateBuilder(source);
        var index = Assert.IsType<CreateIndex.Standard>(ParseStatement("CREATE INDEX ix ON items USING HASH (id)")) with
        {
            Columns = [new TestKeyPart()]
        };

        var error = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.AddIndex(index, null));

        Assert.Equal(source, error.ObjectSourceRef);
    }

    [Theory]
    [InlineData("PRIMARY KEY (id, id)")]
    [InlineData("CONSTRAINT pk PRIMARY KEY (id, id)")]
    public void PrimaryKeys_RejectDuplicateColumnsWithOrWithoutConstraintName(string constraint)
    {
        var builder = CreateBuilder();
        var table = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT, {constraint})"));

        Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() => builder.ApplyCreateTable(table));
    }

    [Theory]
    [InlineData("other.items.id")]
    [InlineData("other.public.items.id")]
    [InlineData("extra.catalog.public.items.id")]
    public void CheckExpressions_RejectWrongSchemaCatalogAndExcessQualification(string reference)
    {
        var builder = CreateBuilder();
        var table = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (id INT, CONSTRAINT ck CHECK ({reference} > 0))"));

        Assert.Throws<NotImplementedException>(() => builder.ApplyCreateTable(table));
    }

    [Fact]
    public void IdentityOptions_RejectUnknownPublicAstVariants()
    {
        var source = new SourceRef(7, 0, 10);
        var builder = CreateBuilder(source);
        builder.AddColumn(new StatementColumn.Typed(new Identifier("id"), new DataType.Int(),
            [new StatementColumnOption.Generated.PgIdentity(true, [new TestSequenceOption()])]), null);

        var error = Assert.Throws<SqlSyntaxException.UnsupportedDialectClause>(() => builder.ToTable(CreateManager()));

        Assert.Equal("identity sequence option TEST", error.Clause);
        Assert.Equal(source, error.ObjectSourceRef);
    }

    [Theory]
    [InlineData("locale", "\"locale\"")]
    [InlineData("App.Locale", "\"app\".\"locale\"")]
    [InlineData("\"C\"", "\"C\"")]
    [InlineData("\"App\".\"Locale\"", "\"App\".\"Locale\"")]
    public void ColumnCollations_PreserveNormalizedNamesAndRoundTrip(string collation, string expected)
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items (label TEXT COLLATE {collation} NOT NULL DEFAULT 'text')"));
        var builder = CreateBuilder();
        builder.ApplyCreateTable(statement);

        PgColumn column = Assert.Single(builder.ToTable(CreateManager()).Columns);

        Assert.Equal(expected, column.Collation?.ToSql());
        StatementColumn.Typed canonical = column.ToStatementColumn();
        Assert.IsType<StatementColumnOption.PgCollation>(canonical.Options[0]);
        var rebuilt = CreateBuilder();
        var reparsed = Assert.IsType<CreateTable>(ParseStatement($"CREATE TABLE items ({canonical.ToSql()})"));
        rebuilt.ApplyCreateTable(reparsed);
        Assert.Equal(column.Collation, Assert.Single(rebuilt.ToTable(CreateManager()).Columns).Collation);
    }

    [Fact]
    public void ColumnCollations_RejectDuplicateManualOptionsWithSource()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (label TEXT COLLATE \"C\")"));
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(statement.Columns));
        StatementColumnOption option = Assert.Single(column.Options);
        var source = new SourceRef(18, 0, 10);
        var builder = CreateBuilder();

        var exception = Assert.Throws<SqlSyntaxException.SpecifiedMoreThanOnce>(() =>
            builder.AddColumn(column with { Options = [option, option] }, source));

        Assert.Equal(source, exception.ObjectSourceRef);
    }

    private static PgTypeResolutionManager CreateManager()
    {
        var config = PgTestConfig.GetTestConfig();
        return new PgTypeResolutionManager(config, new Dictionary<ObjectHandle, ObjectIdentifier>());
    }

    private static PgTableBuilder CreateBuilder(SourceRef? sourceRef = null)
    {
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted);
        return new PgTableBuilder(normalizer.NormalizeObjectIdentifier(ObjectIdentifier.FromStrings("catalog", "public", "items")),
            NameHandling.LowercaseUnlessQuoted, normalizer, sourceRef);
    }

    private sealed class DefaultValueNormalizer(Value normalizedValue)
        : ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted)
    {
        public override Value NormalizeValue(Value value)
        {
            return normalizedValue;
        }
    }

    private record TestKeyPart : KeyPart
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TEST");
        }

        public override void FormatSql(SqlTextWriter writer, Core.Formatting.FormatManager manager)
        {
            ToSql(writer);
        }
    }

    private record TestSequenceOption : PgStatementSequenceOption
    {
        public override string UniqueName => "TEST";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TEST");
        }
    }

    private record TestTableOption : StatementTableOption
    {
        public override string DuplicateCheckKey
        {
            get
            {
                return "test";
            }
        }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("TEST");
        }
    }
}
