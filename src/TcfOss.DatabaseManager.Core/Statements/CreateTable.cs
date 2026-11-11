using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateTable(ObjectName Name, SqlValueList<StatementColumn> Columns, SqlValueList<StatementTableConstraint> Constraints) : Statement, ICreateStatement
{
    public bool OrReplace { get; init; }
    public TemporaryLabel? Temporary { get; init; }
    public bool IfNotExists { get; init; }
    public PgCreateTableOptions? PgOptions { get; init; }
    public bool PrintEmptyBody { get; init; }

    public bool PrintAs { get; init; }
    public Select? AsSelect { get; init; }

    public SqlValueList<StatementTableOption> TableOptions { get; init; } = [];

    public override void ToSql(SqlTextWriter writer)
    {
        string? orReplace = OrReplace ? "OR REPLACE " : null;
        string? temporary = Temporary != null ? $"{Temporary} " : null;
        string? unlogged = PgOptions is { Unlogged: true } ? "UNLOGGED " : null;
        string? ifNotExists = IfNotExists ? "IF NOT EXISTS " : null;

        writer.WriteSql($"CREATE {orReplace}{temporary}{unlogged}TABLE {ifNotExists}{Name}");

        if (PgOptions?.PartitionOf != null)
        {
            writer.WriteSql($" PARTITION OF {PgOptions.PartitionOf.Parent}");
        }

        bool anyColumns = Columns.Any();
        bool anyBody = anyColumns || Constraints.Any() || PrintEmptyBody;
        if (anyBody)
        {
            writer.Write(" (");
        }
        if (anyColumns)
        {
            writer.WriteSql($"{Columns}");
        }
        if (Constraints.Any())
        {
            if (anyColumns)
            {
                writer.Write(", ");
            }
            writer.WriteSql($"{Constraints}");
        }
        if (anyBody)
        {
            writer.Write(")");
        }

        if (PgOptions?.PartitionOf != null)
        {
            writer.WriteSql($" {PgOptions.PartitionOf.Bound}");
        }
        if (PgOptions?.PartitionBy != null)
        {
            writer.WriteSql($" {PgOptions.PartitionBy}");
        }

        if (TableOptions.Any())
        {
            writer.Write(" ");
            writer.WriteDelimited(TableOptions, " ");
        }

        if (PgOptions?.OnCommit != null)
        {
            writer.WriteSql($" ON COMMIT {PgOptions.OnCommit}");
        }

        if (AsSelect != null)
        {
            if (PrintAs)
            {
                writer.Write(" AS");
            }
            writer.WriteSql($" {AsSelect}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        string? orReplace = OrReplace ? "OR REPLACE " : null;
        string? temporary = Temporary != null ? $"{Temporary} " : null;
        string? unlogged = PgOptions is { Unlogged: true } ? "UNLOGGED " : null;
        string? ifNotExists = IfNotExists ? "IF NOT EXISTS " : null;

        writer.WriteSql($"{manager.Indent}CREATE {orReplace}{temporary}{unlogged}TABLE {ifNotExists}");
        Name.FormatSql(writer, manager);

        if (PgOptions?.PartitionOf != null)
        {
            writer.Write(" PARTITION OF ");
            PgOptions.PartitionOf.Parent.FormatSql(writer, manager);
        }

        if (Columns.Any() || Constraints.Any())
        {
            CreateTableFormatter.FormatBody(writer, manager, Columns, Constraints);
        }
        else if (PrintEmptyBody)
        {
            writer.Write(" ()");
        }

        if (PgOptions?.PartitionOf != null)
        {
            writer.Write(" ");
            PgOptions.PartitionOf.Bound.FormatSql(writer, manager);
        }
        if (PgOptions?.PartitionBy != null)
        {
            writer.Write(" ");
            PgOptions.PartitionBy.FormatSql(writer, manager);
        }

        if (TableOptions.Any())
        {
            CreateTableFormatter.FormatTableOptions(writer, manager, TableOptions);
        }

        if (PgOptions?.OnCommit != null)
        {
            writer.WriteSql($" ON COMMIT {PgOptions.OnCommit}");
        }

        if (AsSelect != null)
        {
            CreateTableFormatter.FormatAsSelect(writer, manager, AsSelect, PrintAs);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (PgOptions?.PartitionOf != null)
        {
            using (context.Enter(ReferencedItemsContext.CreateTablePartitionParent))
            {
                ItemRef? parent = context.CreateObjectRef(PgOptions.PartitionOf.Parent);
                if (parent != null)
                {
                    yield return parent;
                }
            }
        }
        using (context.Enter(ReferencedItemsContext.CreateTablePartitionExpression))
        {
            foreach (PgPartitionKey key in PgOptions?.PartitionBy?.Keys ?? [])
            {
                foreach (ItemRef item in key.Expression.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>())
                {
                    yield return item;
                }
            }
            if (PgOptions?.PartitionOf != null)
            {
                foreach (ItemRef item in PgOptions.PartitionOf.Bound.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
            foreach (StatementColumn.PgPartitionOverride column in Columns.OfType<StatementColumn.PgPartitionOverride>())
            {
                foreach (Expression expression in column.Options.Select(option => option switch
                {
                    StatementColumnOption.Default.DefaultExpression value => value.Expression,
                    StatementColumnOption.Generated.AsExpression generated => generated.Expression,
                    _ => null
                }).OfType<Expression>())
                {
                    foreach (ItemRef item in expression.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
        foreach (StatementColumnOption option in Columns.SelectMany(column => column.Options))
        {
            if (option is StatementColumnOption.PgReference reference)
            {
                using (context.Enter(ReferencedItemsContext.ConstraintTable))
                {
                    ItemRef? item = context.CreateObjectRef(reference.Table);
                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
            if (option is StatementColumnOption.CheckConstraint check)
            {
                using (context.Enter(ReferencedItemsContext.ConstraintExpression))
                {
                    foreach (ItemRef item in check.Expression.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>())
                    {
                        yield return item;
                    }
                }
            }
        }
        foreach (StatementTableConstraint constraint in Constraints)
        {
            foreach (ItemRef item in constraint.GetReferencedItems(context))
            {
                yield return item;
            }
        }
        if (AsSelect != null)
        {
            foreach (ItemRef item in AsSelect.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
