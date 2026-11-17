using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record Close : Statement
{
    // public abstract void ToSql(SqlTextWriter writer);
    // public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Named(Identifier Identifier) : Close
    {
        public bool Global { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CLOSE {(Global ? "GLOBAL " : "")}{Identifier}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI($"CLOSE {(Global ? "GLOBAL " : "")}");
            Identifier.FormatSql(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record All : Close
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("CLOSE ALL");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("CLOSE ALL");
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
