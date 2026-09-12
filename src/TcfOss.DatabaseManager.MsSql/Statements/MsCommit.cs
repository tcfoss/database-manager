using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL <c>COMMIT [ { TRAN | TRANSACTION } [ transaction_name ] ]</c>.
/// </summary>
public record MsCommit() : Statement
{
    public TransactionNoun? Noun { get; init; }
    public Identifier? Name { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("COMMIT");
        if (Noun != null)
        {
            writer.WriteSql($" {Noun}");
        }
        if (Name != null)
        {
            writer.WriteSql($" {Name}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("COMMIT");
        if (Noun != null)
        {
            writer.WriteSql($" {Noun}");
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
