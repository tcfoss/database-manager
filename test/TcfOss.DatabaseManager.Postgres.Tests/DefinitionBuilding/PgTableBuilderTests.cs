using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
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
using TcfOss.DatabaseManager.Postgres.Configuration;
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
        Assert.Throws<NotImplementedException>(() => builder.ToTable());
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
        var config = new PgConfig
        {
            ProjectDirectory = ".",
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            QuoteStyle = QuoteStyle.Ansi,
            NameHandling = NameHandling.LowercaseUnlessQuoted
        };
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
        var config = new PgConfig
        {
            ProjectDirectory = ".",
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            QuoteStyle = QuoteStyle.Ansi,
            NameHandling = NameHandling.LowercaseUnlessQuoted
        };
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
    [InlineData("CREATE TABLE items (id INT, PRIMARY KEY (id DESC))")]
    [InlineData("CREATE TABLE items (id INT, PRIMARY KEY ((id + 1)))")]
    public void AddPrimaryKey_RejectsNonColumnKeyPartsWithSourcedSyntaxError(string sql)
    {
        var table = Assert.IsType<CreateTable>(ParseStatement(sql));
        var constraint = Assert.IsType<StatementTableConstraint.PrimaryKey>(Assert.Single(table.Constraints));
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

    private static PgTypeResolutionManager CreateManager()
    {
        var config = new PgConfig
        {
            ProjectDirectory = ".",
            Catalog = new CatalogIdentifier("catalog"),
            DatabaseAvailable = false,
            ValidationSettings = new ValidationSettings(),
            QuoteStyle = QuoteStyle.Ansi,
            NameHandling = NameHandling.LowercaseUnlessQuoted
        };
        return new PgTypeResolutionManager(config, new Dictionary<ObjectHandle, ObjectIdentifier>());
    }

    private static PgTableBuilder CreateBuilder()
    {
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted);
        return new PgTableBuilder(normalizer.NormalizeObjectIdentifier(ObjectIdentifier.FromStrings("catalog", "public", "items")),
            NameHandling.LowercaseUnlessQuoted, normalizer, null);
    }

    private sealed class DefaultValueNormalizer(Value normalizedValue)
        : ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted)
    {
        public override Value NormalizeValue(Value value)
        {
            return normalizedValue;
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
