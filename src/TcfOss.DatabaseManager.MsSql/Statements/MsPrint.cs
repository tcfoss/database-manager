using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL <c>PRINT &lt;expression&gt;</c>: writes the result of a string or
/// numeric expression as a message to the client.
/// </summary>
public record MsPrint(Expression Body) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"PRINT {Body}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("PRINT ");
        Body.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Body.GetReferencedItems(context);
    }
}
