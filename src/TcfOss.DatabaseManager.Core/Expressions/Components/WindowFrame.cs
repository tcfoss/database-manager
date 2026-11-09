using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions.Components;

public record WindowFrame(WindowFrameUnit Unit, WindowFrameBound Start, WindowFrameBound? End = null) : IWriteSql
{
    public WindowFrameExclusion? Exclusion { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Unit} ");
        if (End is null)
        {
            writer.WriteSql($"{Start}");
        }
        else
        {
            writer.WriteSql($"BETWEEN {Start} AND {End}");
        }
        if (Exclusion is not null)
        {
            writer.WriteSql($" EXCLUDE {Exclusion}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSql($"{Unit} ");
        if (End is not null)
        {
            writer.Write("BETWEEN ");
        }
        Start.FormatSql(writer, manager);
        if (End is not null)
        {
            writer.Write(" AND ");
            End.FormatSql(writer, manager);
        }
        if (Exclusion is not null)
        {
            writer.WriteSql($" EXCLUDE {Exclusion}");
        }
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Start.GetReferencedItems(context))
        {
            yield return item;
        }
        if (End is not null)
        {
            foreach (ItemRef item in End.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
