using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record PgIntervalLiteral(Value Value, uint? Precision = null, PgIntervalQualifier? Qualifier = null) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("INTERVAL");
        if (Precision != null)
        {
            writer.WriteSql($"({Precision})");
        }
        writer.WriteSql($" {Value}");
        if (Qualifier != null)
        {
            writer.WriteSql($" {Qualifier}");
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
