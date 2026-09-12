using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record LabeledStatement(Statement SubStatement, Identifier Label) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Label}: {SubStatement}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.Write(manager.Indent);
        Label.FormatSql(writer, manager);
        writer.Write(": ");
        SubStatement.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in SubStatement.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
