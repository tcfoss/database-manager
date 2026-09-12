using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record TypedString(string Value, DataType DataType) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        DataType.ToSql(writer);
        writer.WriteSql($" '{Value.EscapeSingleQuotedString()}'");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
