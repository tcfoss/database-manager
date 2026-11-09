using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Expressions.Components;

/// <summary>
/// A named window reference or an inline specification, including inheritance
/// and optional framing.
/// </summary>
public abstract record WindowSpec : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record NamedReference(Identifier WindowName) : WindowSpec
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{WindowName}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            WindowName.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }

    public record InlineSpecification : WindowSpec
    {
        public Identifier? BaseWindow { get; init; }
        public SqlValueList<Expression>? PartitionBy { get; init; }
        public SqlValueList<OrderBy>? OrderBy { get; init; }
        public WindowFrame? Frame { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("(");
            DelimitedWriter delimited = new(writer, " ");
            if (BaseWindow != null)
            {
                delimited.WriteSql($"{BaseWindow}");
            }
            if (PartitionBy != null)
            {
                delimited.WriteSql($"PARTITION BY {PartitionBy.ToSqlDelimited()}");
            }
            if (OrderBy != null)
            {
                delimited.WriteSql($"ORDER BY {OrderBy.ToSqlDelimited()}");
            }
            if (Frame != null)
            {
                delimited.WriteSql($"{Frame}");
            }
            writer.Write(")");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("(");
            DelimitedWriter delimited = new(writer, " ");
            if (BaseWindow != null)
            {
                delimited.FormatSql(BaseWindow, manager);
            }
            if (PartitionBy != null)
            {
                delimited.Write("PARTITION BY ");
                writer.FormatDelimited(PartitionBy, manager);
            }
            if (OrderBy != null)
            {
                delimited.Write("ORDER BY ");
                for (int index = 0; index < OrderBy.Count; index++)
                {
                    if (index > 0)
                    {
                        writer.Write(", ");
                    }
                    OrderBy[index].FormatInline(writer, manager);
                }
            }
            if (Frame != null)
            {
                delimited.FormatSql(Frame, manager);
            }
            writer.Write(")");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            if (Frame != null)
            {
                foreach (ItemRef item in Frame.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
            if (PartitionBy != null)
            {
                foreach (Expression expr in PartitionBy)
                {
                    foreach (ItemRef item in expr.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
            if (OrderBy != null)
            {
                foreach (OrderBy ob in OrderBy)
                {
                    foreach (ItemRef item in ob.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
