using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgAssert(Expression Condition, Expression? Message) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"ASSERT {Condition}");
        if (Message != null)
        {
            writer.WriteSql($", {Message}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("ASSERT ");
        Condition.FormatSql(writer, manager);
        if (Message != null)
        {
            writer.Write(", ");
            Message.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Condition.GetReferencedItems(context))
        {
            yield return item;
        }
        if (Message != null)
        {
            foreach (ItemRef item in Message.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
