using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.MsSql.Statements.Components;

public record MsThrowable(Expression ErrorNumber, Expression Message, Expression State) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{ErrorNumber}, {Message}, {State}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ErrorNumber.FormatSql(writer, manager);
        writer.Write(", ");
        Message.FormatSql(writer, manager);
        writer.Write(", ");
        State.FormatSql(writer, manager);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in ErrorNumber.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in Message.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in State.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
