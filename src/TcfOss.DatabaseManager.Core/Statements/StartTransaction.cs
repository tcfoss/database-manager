using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public record StartTransaction() : Statement
{
    public SqlValueList<MySqlTransactionCharacteristic>? Characteristics { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("START TRANSACTION");
        if (Characteristics != null)
        {
            foreach (MySqlTransactionCharacteristic characteristic in Characteristics)
            {
                writer.WriteSql($" {characteristic}");
            }
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("START TRANSACTION");
        if (Characteristics != null)
        {
            foreach (MySqlTransactionCharacteristic characteristic in Characteristics)
            {
                writer.WriteSql($" {characteristic}");
            }
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
