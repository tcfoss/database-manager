using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgTable(ObjectIdentifier Name, SqlValueList<PgColumn> Columns) : IDatabaseObject<CreateTable>
{
    public bool Unlogged { get; init; }
    public Identifier? AccessMethod { get; init; }
    public Identifier? Tablespace { get; init; }
    public PgPartitioning? PartitionBy { get; init; }
    public PgTablePartition? PartitionOf { get; init; }
    public PgPrimaryKey? PrimaryKey { get; init; }
    public DatabaseComponentDict<PgTableConstraint.Unique> UniqueConstraints { get; init; } = [];
    public DatabaseComponentDict<PgTableConstraint.ForeignKey> ForeignKeys { get; init; } = [];
    public DatabaseComponentDict<PgTableConstraint.Check> Checks { get; init; } = [];
    public DatabaseComponentDict<PgTableConstraint.Exclusion> Exclusions { get; init; } = [];
    public DatabaseComponentDict<PgIndex> Indexes { get; init; } = [];

    public IEnumerable<CreateIndex> ToCreateIndexStatements(bool includeSchema, DifferFormatManager manager)
    {
        return Indexes.Values.Select(index => index.ToCreateStatement(Name, includeSchema, manager));
    }

    public ObjectType ObjectType => ObjectType.Table;

    public CreateTable ToCreateStatement(bool includeSchema, DifferFormatManager manager)
    {
        var constraints = new SqlValueList<StatementTableConstraint>();
        if (PrimaryKey != null)
        {
            constraints.Add(PrimaryKey.ToStatementConstraint());
        }

        bool omitModifiersIfDefault = manager.Formatting.OmitModifiersIfDefault;
        constraints.AddRange(Checks.Values.Select(constraint => constraint.ToStatementConstraint(omitModifiersIfDefault)));
        constraints.AddRange(ForeignKeys.Values.Select(foreignKey => foreignKey.ToStatementConstraint(omitModifiersIfDefault)));
        constraints.AddRange(UniqueConstraints.Values.Select(unique => unique.ToStatementConstraint(omitModifiersIfDefault)));
        constraints.AddRange(Exclusions.Values.Select(exclusion => exclusion.ToStatementConstraint(omitModifiersIfDefault)));

        SqlValueList<StatementColumn> columns = Columns.Map<StatementColumn>(column =>
        {
            StatementColumn.Typed typed = column.ToStatementColumn(manager);
            return PartitionOf == null ? typed : new StatementColumn.PgPartitionOverride(typed.Name,
                [.. typed.Options.Where(option => option is not (StatementColumnOption.PgStorage
                    or StatementColumnOption.PgCompression or StatementColumnOption.PgCollation))]);
        });
        return new CreateTable(Name.ToObjectName(includeSchema ? 2 : 1), columns, constraints)
        {
            PgOptions = Unlogged || PartitionBy != null || PartitionOf != null || AccessMethod != null || Tablespace != null
                ? new PgCreateTableOptions(Unlogged, null)
                {
                    PartitionBy = PartitionBy?.ToStatementPartitionBy(),
                    PartitionOf = PartitionOf?.ToStatementPartitionOf(includeSchema),
                    AccessMethod = AccessMethod,
                    Tablespace = Tablespace
                }
                : null
        };
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (PartitionOf != null)
        {
            using (context.Enter(ReferencedItemsContext.CreateTablePartitionParent))
            {
                ItemRef? parent = context.CreateObjectRef(PartitionOf.Parent.ToObjectName(2));
                if (parent != null)
                {
                    yield return parent;
                }
            }
        }
        using (context.Enter(ReferencedItemsContext.CreateTablePartitionExpression))
        {
            foreach (ItemRef item in GetExpressionReferences(PartitionBy?.Keys.Select(key => key.Expression) ?? [], context))
            {
                yield return item;
            }
            IEnumerable<Expression> bounds = PartitionOf?.Bound switch
            {
                PgPartitionBounds.List list => list.Values,
                PgPartitionBounds.Range range => range.From.Concat(range.To)
                    .OfType<PgPartitionBounds.RangeValue.ExpressionValue>().Select(value => value.Expression),
                _ => []
            };
            foreach (ItemRef item in GetExpressionReferences(bounds, context))
            {
                yield return item;
            }
        }
        using (context.Enter(ReferencedItemsContext.AlterColumnExpression))
        {
            foreach (PgColumn column in Columns)
            {
                if (column.Default is ColumnOption.ColumnDefault.DefaultExpression defaultExpression)
                {
                    foreach (ItemRef item in GetExpressionReferences([defaultExpression.Expression], context))
                    {
                        yield return item;
                    }
                }
                if (column.Generated is ColumnOption.Generated.AsExpression generated)
                {
                    foreach (ItemRef item in GetExpressionReferences([generated.Expression], context))
                    {
                        yield return item;
                    }
                }
            }
        }
        using (context.Enter(ReferencedItemsContext.ConstraintTable))
        {
            foreach (PgTableConstraint.ForeignKey foreignKey in ForeignKeys.Values)
            {
                ItemRef? table = context.CreateObjectRef(foreignKey.ReferencedTable.ToObjectName(2));
                if (table != null)
                {
                    yield return table;
                }
            }
        }
        using (context.Enter(ReferencedItemsContext.ConstraintExpression))
        {
            foreach (ItemRef item in GetExpressionReferences(Checks.Values.Select(check => check.Expression), context))
            {
                yield return item;
            }
            foreach (PgTableConstraint.Exclusion exclusion in Exclusions.Values)
            {
                foreach (ItemRef item in GetExpressionReferences(
                    GetIndexExpressions(exclusion.Elements.Select(element => element.Key), exclusion.BackingIndex.Predicate), context))
                {
                    yield return item;
                }
            }
        }
        foreach (PgIndex index in Indexes.Values)
        {
            using (context.Enter(ReferencedItemsContext.CreateIndexTable))
            {
                ItemRef? table = context.CreateObjectRef(Name.ToObjectName(2));
                if (table != null)
                {
                    yield return table;
                }
            }
            using (context.Enter(ReferencedItemsContext.CreateIndexExpression))
            {
                foreach (ItemRef item in GetExpressionReferences(GetIndexExpressions(index.Columns, index.Predicate), context))
                {
                    yield return item;
                }
            }
        }
    }

    private static IEnumerable<Expression> GetIndexExpressions(IEnumerable<PgKeyPart> keys, Expression? predicate)
    {
        foreach (PgKeyPart.IndexExpression key in keys.OfType<PgKeyPart.IndexExpression>())
        {
            yield return key.Expression;
        }
        if (predicate != null)
        {
            yield return predicate;
        }
    }

    private static IEnumerable<ItemRef.ObjectNameRef> GetExpressionReferences(IEnumerable<Expression> expressions,
        ReferencedItemsManager context)
    {
        return expressions.SelectMany(expression => expression.GetReferencedItems(context)).OfType<ItemRef.ObjectNameRef>();
    }

    public PseudoTable ToPseudoTable()
    {
        return new PseudoTable(Name.Name, Name, [.. Columns.Select(column => column.Name.Name)], PseudoTableType.Table);
    }
}
