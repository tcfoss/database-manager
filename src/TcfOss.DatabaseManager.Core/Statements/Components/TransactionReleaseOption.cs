using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record TransactionReleaseOption() : IWriteSql
{
    public bool Negated { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        if (Negated)
        {
            writer.Write("NO ");
        }
        writer.Write("RELEASE");
    }
}
