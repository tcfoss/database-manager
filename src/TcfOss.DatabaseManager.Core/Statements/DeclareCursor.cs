using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public record DeclareCursor(Identifier Name, Statement Query) : Statement
{
    public bool Binary { get; init; }
    public PgCursorSensitivity? Sensitivity { get; init; }
    public PgCursorScrollMode? ScrollMode { get; init; }
    public PgCursorHoldMode? Holdability { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"DECLARE {Name}");
        if (Binary)
        {
            writer.Write(" BINARY");
        }
        if (Sensitivity is not null)
        {
            writer.WriteSql($" {Sensitivity}");
        }
        if (ScrollMode is not null)
        {
            writer.WriteSql($" {ScrollMode}");
        }
        writer.Write(" CURSOR");
        if (Holdability is not null)
        {
            writer.WriteSql($" {Holdability}");
        }
        writer.WriteSql($" FOR {Query}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("DECLARE ");
        Name.FormatSql(writer, manager);
        if (Binary)
        {
            writer.Write(" BINARY");
        }
        if (Sensitivity is not null)
        {
            writer.Write(" ");
            Sensitivity.ToSql(writer);
        }
        if (ScrollMode is not null)
        {
            writer.Write(" ");
            ScrollMode.ToSql(writer);
        }
        bool hasModifiersBeforeCursor = Binary || Sensitivity is not null || ScrollMode is not null;
        if (hasModifiersBeforeCursor)
        {
            writer.WriteLine();
            manager.IncreaseIndent();
        }
        else
        {
            writer.Write(" ");
        }

        writer.WriteSqlI("CURSOR");
        if (Holdability is not null)
        {
            writer.WriteSql($" {Holdability}");
        }
        writer.WriteLine(" FOR");
        if (!hasModifiersBeforeCursor)
        {
            manager.IncreaseIndent();
        }
        Query.FormatSql(writer, manager);
        manager.DecreaseIndent();
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Query.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
