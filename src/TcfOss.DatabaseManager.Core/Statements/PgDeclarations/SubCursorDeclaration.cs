using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements.PgDeclarations;

public record SubCursorDeclaration(
    Identifier Name,
    PgCursorScrollMode? ScrollMode,
    SqlValueList<PgCursorParameter>? Parameters,
    bool UseIs,
    Statement Query) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name}");
        if (ScrollMode != null)
        {
            writer.WriteSql($" {ScrollMode}");
        }
        writer.Write(" CURSOR");
        if (Parameters != null)
        {
            writer.Write("(");
            writer.WriteDelimited(Parameters);
            writer.Write(")");
        }
        writer.Write(UseIs ? " IS " : " FOR ");
        Query.ToSql(writer);
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        Name.FormatSql(writer, manager);
        if (ScrollMode != null)
        {
            writer.Write(" ");
            ScrollMode.ToSql(writer);
        }
        writer.Write(" CURSOR");
        if (Parameters != null)
        {
            writer.Write("(");
            writer.FormatDelimited(Parameters, manager);
            writer.Write(")");
        }
        writer.Write(UseIs ? " IS " : " FOR ");
        Query.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Query.GetReferencedItems(context);
    }
}
