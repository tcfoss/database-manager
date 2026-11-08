using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record PositionalParameter(int Number) : Expression
{
    public SourceRef? Source { get; init; }

    public virtual bool Equals(PositionalParameter? other)
    {
        return other is not null && base.Equals(other) && Number == other.Number;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Number);
    }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"${Number}");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
