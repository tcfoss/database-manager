using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record SetConstraints(bool Deferred) : Statement
{
    public record All(bool Deferred) : SetConstraints(Deferred)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET CONSTRAINTS ALL {Timing}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI($"SET CONSTRAINTS ALL {Timing}");
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record Named(SqlValueList<ObjectName> Names, bool Deferred) : SetConstraints(Deferred)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET CONSTRAINTS {Names.ToSqlDelimited()} {Timing}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.Write("SET CONSTRAINTS ");
            writer.FormatDelimited(Names, manager);
            writer.WriteSqlI($" {Timing}");
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    protected string Timing => Deferred ? "DEFERRED" : "IMMEDIATE";

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
