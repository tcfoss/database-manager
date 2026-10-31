using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgTableBuilder(ObjectIdentifier name, NameHandling nameHandling, INormalizeComponents componentNormalizer, SourceRef? sourceRef)
    : TableBuilder<PgTable>(name, nameHandling, componentNormalizer, sourceRef)
{
    private readonly List<StatementColumn.Typed> _columns = [];

    public IReadOnlyList<StatementColumn.Typed> Columns
    {
        get
        {
            return _columns;
        }
    }

    public override void AddColumn(StatementColumn column, SourceRef? sourceRef)
    {
        if (column is not StatementColumn.Typed typedColumn)
        {
            throw new NotImplementedException("PostgreSQL column construction currently supports typed declarations only.");
        }
        if (!ColumnNames.Add(Handle.GetNamePart(typedColumn.Name, NameHandling)))
        {
            throw new SqlSyntaxException.DuplicateColumn(typedColumn.Name.Name, Name, sourceRef ?? SourceRef);
        }
        _columns.Add(typedColumn);
    }

    public override void AddIndexOrConstraint(StatementTableConstraint constraint, SourceRef? sourceRef)
    {
        throw new NotImplementedException();
    }

    public override void AddOption(StatementTableOption tableOption)
    {
        throw new NotImplementedException();
    }

    public override PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. _columns.Select(column => Handle.GetNamePart(column.Name, NameHandling))], PseudoTableType.Table);
    }

    public override PgTable ToTable()
    {
        throw new NotImplementedException("PostgreSQL table construction requires a path and type resolution manager.");
    }

    public PgTable ToTable(PgTypeResolutionManager manager)
    {
        ObjectIdentifier name = manager.GetName(Name);
        SqlValueList<PgColumn> columns = [];
        foreach (StatementColumn.Typed column in _columns)
        {
            Identifier columnName = ComponentNormalizer.NormalizeSingleIdentifier(column.Name);
            ColumnOption.Nullability? nullability = null;
            ColumnOption.ColumnDefault? defaultValue = null;
            var seenOptions = new HashSet<string>();
            foreach (StatementColumnOption option in column.Options)
            {
                if (!seenOptions.Add(option.DuplicateCheckKey))
                {
                    throw new SqlSyntaxException.SpecifiedMoreThanOnce(option.DuplicateCheckKey, "column", column.Name, SourceRef);
                }
                switch (option)
                {
                    case StatementColumnOption.Nullability nullabilityOption:
                        Identifier? nullabilityName = ComponentNormalizer.NormalizeSingleIdentifierNullable(nullabilityOption.Name);
                        nullability = nullabilityOption is StatementColumnOption.Nullability.NotNull
                            ? new ColumnOption.Nullability.NotNull(nullabilityName)
                            : new ColumnOption.Nullability.Null(nullabilityName);
                        break;
                    case StatementColumnOption.Default defaultOption:
                        defaultValue = NormalizeDefault(defaultOption);
                        break;
                    default:
                        throw new NotImplementedException($"PostgreSQL column option {option.GetType().Name} is not implemented.");
                }
            }
            columns.Add(new PgColumn(new ColumnIdentifier(columnName.Name, name, columnName.QuoteStyle)
            {
                Source = columnName.Source,
                OriginalIdentifiers = columnName.OriginalIdentifiers
            }, manager.Resolve(column.DataType, Name.Schema, SourceRef))
            {
                Nullability = nullability,
                Default = defaultValue
            });
        }
        return new PgTable(name, columns);
    }

    protected override DataType GetNormalizedDataType(DataType dataType)
    {
        throw new NotImplementedException();
    }

    protected override Expression GetNormalizedExpression(Expression expression)
    {
        return expression;
    }

    protected override (Expression Normalized, Expression Flattened) GetNormalizedExpressions(Expression expression)
    {
        return (expression, expression);
    }
}
