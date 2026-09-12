using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record DeclareCursor(Identifier Name, Select Query) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"DECLARE {Name} CURSOR FOR {Query}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("DECLARE ");
        Name.FormatSql(writer, manager);
        writer.Write(" CURSOR FOR");
        writer.WriteLine();
        manager.IncreaseIndent();
        Query.FormatSql(writer, manager);
        manager.DecreaseIndent();
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Query.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
