using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public record StartTransaction() : Statement
{
    public record Begin(TransactionNoun? Noun) : StartTransaction
    {
        protected override string Command => Noun == null ? "BEGIN" : $"BEGIN {Noun}";
    }

    protected virtual string Command => "START TRANSACTION";

    public SqlValueList<TransactionMode> Modes { get; init; } = [];

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write(Command);
        WriteCharacteristics(writer);
    }

    private void WriteCharacteristics(SqlTextWriter writer)
    {
        if (Modes.Count > 0)
        {
            writer.WriteSql($" {Modes}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI(Command);
        WriteCharacteristics(writer);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
