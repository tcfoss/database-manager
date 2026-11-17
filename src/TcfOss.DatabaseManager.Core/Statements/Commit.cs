using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Commit() : Statement
{
    public TransactionNoun? Noun { get; init; }
    public TransactionChainOption? ChainOption { get; init; }
    public TransactionReleaseOption? ReleaseOption { get; init; }
    public Identifier? Name { get; init; }

    public bool WriteAsEnd { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write(WriteAsEnd ? "END" : "COMMIT");
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
        if (Name != null)
        {
            writer.WriteSql($" {Name}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI(WriteAsEnd ? "END" : "COMMIT");
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
        if (Name != null)
        {
            writer.Write(" ");
            Name.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
