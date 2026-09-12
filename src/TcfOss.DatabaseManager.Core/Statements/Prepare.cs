using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record Prepare(Identifier StatementName) : Statement
{
    public record FromString(Identifier StatementName, Value.SingleQuotedString StatementString) : Prepare(StatementName)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"PREPARE {StatementName} FROM {StatementString}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("PREPARE ");
            StatementName.FormatSql(writer, manager);
            writer.WriteSql($" FROM {StatementString}");
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }

    public record FromVariable(Identifier StatementName, Identifier StatementVariable) : Prepare(StatementName)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"PREPARE {StatementName} FROM {StatementVariable}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("PREPARE ");
            StatementName.FormatSql(writer, manager);
            writer.Write(" FROM ");
            StatementVariable.FormatSql(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }
}
