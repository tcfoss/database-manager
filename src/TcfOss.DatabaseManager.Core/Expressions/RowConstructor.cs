using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record RowConstructor(SqlValueList<Expression> Fields) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"ROW({Fields})");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write("ROW(");
        for (int index = 0; index < Fields.Count; index++)
        {
            if (index > 0)
            {
                writer.Write(", ");
            }
            Fields[index].FormatSql(writer, manager);
        }
        writer.Write(")");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Expression field in Fields)
        {
            foreach (ItemRef item in field.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
