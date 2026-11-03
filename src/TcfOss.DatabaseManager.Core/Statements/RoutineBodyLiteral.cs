using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record RoutineBodyLiteral(string Value) : Statement
{
    public string? DollarQuoteTag { get; init; }

    public virtual bool Equals(RoutineBodyLiteral? other)
    {
        return other != null && base.Equals(other) && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Value);
    }

    public override void ToSql(SqlTextWriter writer)
    {
        if (DollarQuoteTag is { } tag)
        {
            writer.Write($"${tag}${Value}${tag}$");
        }
        else
        {
            writer.Write($"'{Value.Replace("'", "''", StringComparison.Ordinal)}'");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.Write(manager.Indent);
        ToSql(writer);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
