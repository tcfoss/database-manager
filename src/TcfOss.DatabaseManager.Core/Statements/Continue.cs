using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements;

/// <summary>
/// T-SQL-style <c>CONTINUE</c>: restarts the enclosing <see cref="While"/>
/// loop. Unlike MySQL <see cref="Iterate"/>, it has no label.
/// </summary>
public record Continue : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("CONTINUE");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("CONTINUE");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
