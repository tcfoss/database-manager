using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record SetTransaction : Statement
{
    public record Characteristics(SqlValueList<TransactionMode> Modes) : SetTransaction
    {
        public bool Session { get; init; }

        private string Command
        {
            get
            {
                return Session ? "SET SESSION CHARACTERISTICS AS TRANSACTION" : "SET TRANSACTION";
            }
        }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Command} {Modes}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI($"{Command} {Modes}");
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record Snapshot(Value.StringBasedValue SnapshotId) : SetTransaction
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET TRANSACTION SNAPSHOT {SnapshotId}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI($"SET TRANSACTION SNAPSHOT {SnapshotId}");
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
