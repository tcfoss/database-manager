using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions.Components;

public abstract record WindowFrameBound : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }

    public record UnboundedPreceding : WindowFrameBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("UNBOUNDED PRECEDING");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("UNBOUNDED PRECEDING");
        }
    }

    public record UnboundedFollowing : WindowFrameBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("UNBOUNDED FOLLOWING");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("UNBOUNDED FOLLOWING");
        }
    }

    public record CurrentRow : WindowFrameBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("CURRENT ROW");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("CURRENT ROW");
        }
    }

    public record Preceding(Expression Offset) : WindowFrameBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Offset} PRECEDING");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Offset.FormatSql(writer, manager);
            writer.Write(" PRECEDING");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Offset.GetReferencedItems(context);
        }
    }

    public record Following(Expression Offset) : WindowFrameBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Offset} FOLLOWING");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Offset.FormatSql(writer, manager);
            writer.Write(" FOLLOWING");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Offset.GetReferencedItems(context);
        }
    }
}
