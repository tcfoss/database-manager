using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.MySql.Statements;

public record SetDelimiter(string Delimiter) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"DELIMITER {Delimiter}");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
