using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
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
    public void ApplyCreateTable_RetainsPendingTypesAndColumnOptions()
    {
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (Id INT, \"Mood\" app.mood DEFAULT 'happy')"));
        var builder = CreateBuilder();

        builder.ApplyCreateTable(statement);

        Assert.Equal(2, builder.Columns.Count);
        Assert.Same(statement.Columns[1], builder.Columns[1]);
        Assert.IsType<DataTypeReference.Named>(builder.Columns[1].DataType);
        Assert.IsType<StatementColumnOption.Default.DefaultValue>(Assert.Single(builder.Columns[1].Options));
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
        var statement = Assert.IsType<CreateTable>(ParseStatement("CREATE TABLE items (id INT, PRIMARY KEY (id))"));
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
        Expression parsedExpression = Assert.IsType<StatementColumnOption.Default.DefaultExpression>(Assert.Single(builder.Columns[4].Options)).Expression;
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

        var table = builder.ToTable(manager);

        var result = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(Assert.Single(table.Columns).Default);
        Assert.Same(expected, result.Value);
        Assert.Equal("defaultname", result.Name?.Name);
    }

    private static PgTableBuilder CreateBuilder()
    {
        return new PgTableBuilder(ObjectIdentifier.FromStrings("catalog", "public", "items"), NameHandling.LowercaseUnlessQuoted,
            new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted), null);
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
