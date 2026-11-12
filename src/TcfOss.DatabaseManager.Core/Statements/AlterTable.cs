using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record AlterTable(ObjectName Table, SqlValueList<AlterTableOperation> Operations) : Statement
{
    public bool IfExists { get; init; }
    public bool Only { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"ALTER TABLE{(IfExists ? " IF EXISTS" : "")}{(Only ? " ONLY" : "")} {Table} {Operations.ToSqlDelimited()}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("ALTER TABLE ");
        if (IfExists)
        {
            writer.Write("IF EXISTS ");
        }
        if (Only)
        {
            writer.Write("ONLY ");
        }
        Table.FormatSql(writer, manager);
        writer.WriteLine();
        manager.IncreaseIndent();
        for (int i = 0; i < Operations.Count; i++)
        {
            Operations[i].FormatSql(writer, manager);
            if (i < Operations.Count - 1)
            {
                writer.WriteLine(",");
            }
        }
        manager.DecreaseIndent();
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.AlterTableTarget))
        {
            ItemRef? item = context.CreateObjectRef(Table);
            if (item != null)
            {
                yield return item;
            }
        }
        foreach (AlterTableOperation operation in Operations)
        {
            StatementTableConstraint? constraint = operation switch
            {
                AlterTableOperation.AddPrimaryKey add => add.PrimaryKey,
                AlterTableOperation.AddUniqueKey add => add.UniqueKey,
                AlterTableOperation.AddForeignKey add => add.ForeignKey,
                AlterTableOperation.AddCheck add => add.Check,
                AlterTableOperation.PgAddConstraint add => add.Constraint,
                _ => null
            };
            if (constraint != null)
            {
                foreach (ItemRef item in constraint.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
            if (operation is AlterTableOperation.AddColumn addColumn)
            {
                using (context.Enter(ReferencedItemsContext.ConstraintTable))
                {
                    foreach (StatementColumnOption.PgReference reference in addColumn.Column.Options.OfType<StatementColumnOption.PgReference>())
                    {
                        ItemRef? item = context.CreateObjectRef(reference.Table);
                        if (item != null)
                        {
                            yield return item;
                        }
                    }
                }
            }
            ObjectName? dependency = operation switch
            {
                AlterTableOperation.PgInherit inherit => inherit.Parent,
                AlterTableOperation.PgOf of => of.Type,
                AlterTableOperation.PgAttachPartition attach => attach.Partition,
                AlterTableOperation.PgDetachPartition detach => detach.Partition,
                _ => null
            };
            using (context.Enter(operation is AlterTableOperation.PgOf
                ? ReferencedItemsContext.AlterTableType : ReferencedItemsContext.AlterTableTarget))
            {
                if (dependency != null)
                {
                    ItemRef? item = context.CreateObjectRef(dependency);
                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
            if (operation is AlterTableOperation.PgAttachPartition attachment)
            {
                using (context.Enter(ReferencedItemsContext.AlterPartitionBound))
                {
                    foreach (ItemRef item in attachment.Bound.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
            DataTypeReference? dataType = operation switch
            {
                AlterTableOperation.AddColumn { Column: StatementColumn.Typed column } => column.DataType,
                AlterTableOperation.PgColumn { Operation: PgAlterColumnOperation.ChangeType change } => change.DataType,
                _ => null
            };
            while (dataType is DataTypeReference.Array array)
            {
                dataType = array.ElementType;
            }
            IEnumerable<ObjectName> collations = operation switch
            {
                AlterTableOperation.PgColumn { Operation: PgAlterColumnOperation.ChangeType { Collation: { } collation } } => [collation],
                AlterTableOperation.AddColumn add => add.Column.Options.OfType<StatementColumnOption.PgCollation>().Select(option => option.Name),
                _ => []
            };
            using (context.Enter(ReferencedItemsContext.AlterColumnType))
            {
                if (dataType is DataTypeReference.Named named)
                {
                    ItemRef? item = context.CreateObjectRef(named.Name);
                    if (item != null)
                    {
                        yield return item;
                    }
                }
                foreach (ObjectName collation in collations)
                {
                    ItemRef? item = context.CreateObjectRef(collation);
                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
            IEnumerable<Expression> expressions = operation switch
            {
                AlterTableOperation.PgColumn { Operation: PgAlterColumnOperation.ChangeType { Using: { } expression } } => [expression],
                AlterTableOperation.PgColumn { Operation: PgAlterColumnOperation.SetExpression expression } => [expression.Expression],
                AlterTableOperation.SetDefault { NewDefault: StatementColumnOption.Default.DefaultExpression expression } => [expression.Expression],
                AlterTableOperation.AddColumn add => add.Column.Options.Select(option => option switch
                {
                    StatementColumnOption.Default.DefaultExpression expression => expression.Expression,
                    StatementColumnOption.Generated.AsExpression generated => generated.Expression,
                    StatementColumnOption.CheckConstraint check => check.Expression,
                    _ => null
                }).OfType<Expression>(),
                _ => []
            };
            using (context.Enter(ReferencedItemsContext.AlterColumnExpression))
            {
                foreach (Expression expression in expressions)
                {
                    foreach (ItemRef item in expression.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
