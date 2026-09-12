using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Rollback() : Statement
{
    public TransactionNoun? Noun { get; init; }
    public TransactionChainOption? ChainOption { get; init; }
    public TransactionReleaseOption? ReleaseOption { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("ROLLBACK");
        if (Noun != null)
        {
            writer.WriteSql($" {Noun}");
        }
        if (ChainOption != null)
        {
            writer.WriteSql($" {ChainOption}");
        }
        if (ReleaseOption != null)
        {
            writer.WriteSql($" {ReleaseOption}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("ROLLBACK");
        if (Noun != null)
        {
            writer.WriteSql($" {Noun}");
        }
        if (ChainOption != null)
        {
            writer.WriteSql($" {ChainOption}");
        }
        if (ReleaseOption != null)
        {
            writer.WriteSql($" {ReleaseOption}");
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
