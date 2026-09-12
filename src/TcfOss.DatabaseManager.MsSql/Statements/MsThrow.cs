using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MsSql.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL <c>THROW [ error_number , message , state ]</c>: raises an exception
/// and transfers execution to a CATCH block. The bare form (no arguments) is
/// only valid inside a CATCH block and rethrows the current exception.
/// </summary>
public record MsThrow(MsThrowable? Throwable) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("THROW");
        if (Throwable != null)
        {
            writer.WriteSql($" {Throwable}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("THROW");
        if (Throwable != null)
        {
            writer.Write(" ");
            Throwable.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Throwable != null)
        {
            foreach (ItemRef item in Throwable.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
