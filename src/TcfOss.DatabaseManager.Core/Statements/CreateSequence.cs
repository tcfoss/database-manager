using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CreateSequence(ObjectName Name, SqlValueList<PgStatementSequenceOption> Options) : Statement, ICreateStatement
{
    public TemporaryLabel? TemporaryLabel { get; init; }
    public bool Unlogged { get; init; }
    public bool IfNotExists { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("CREATE ");
        if (TemporaryLabel != null)
        {
            writer.WriteSql($"{TemporaryLabel} ");
        }
        if (Unlogged)
        {
            writer.Write("UNLOGGED ");
        }
        writer.Write("SEQUENCE ");
        if (IfNotExists)
        {
            writer.Write("IF NOT EXISTS ");
        }
        Name.ToSql(writer);
        foreach (PgStatementSequenceOption option in Options)
        {
            writer.WriteSql($" {option}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("CREATE ");
        if (TemporaryLabel != null)
        {
            writer.WriteSql($"{TemporaryLabel} ");
        }
        if (Unlogged)
        {
            writer.Write("UNLOGGED ");
        }
        writer.Write("SEQUENCE ");
        if (IfNotExists)
        {
            writer.Write("IF NOT EXISTS ");
        }
        Name.FormatSql(writer, manager);
        foreach (PgStatementSequenceOption option in Options)
        {
            writer.Write(" ");
            option.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Options.OfType<PgStatementSequenceOption.OwnedBy>().SelectMany(option => option.GetReferencedItems(context));
    }
}
