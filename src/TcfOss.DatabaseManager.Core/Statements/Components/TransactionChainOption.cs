using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record TransactionChainOption() : IWriteSql
{
    public bool Negated { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write("AND");
        if (Negated)
        {
            writer.Write(" NO");
        }
        writer.Write(" CHAIN");
    }
}
