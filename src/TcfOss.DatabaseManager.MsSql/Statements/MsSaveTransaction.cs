using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL savepoint declaration: <c>SAVE { TRAN | TRANSACTION } savepoint_name</c>.
/// </summary>
public record MsSaveTransaction(TransactionNoun Noun, Identifier Name) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"SAVE {Noun} {Name}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI($"SAVE {Noun} ");
        Name.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
