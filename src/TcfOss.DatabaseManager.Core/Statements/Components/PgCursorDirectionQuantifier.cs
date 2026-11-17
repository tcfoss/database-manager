using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgCursorDirectionQuantifier : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }
    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }

    public record All() : PgCursorDirectionQuantifier
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("ALL");
        }
    }

    public record Specified(Expression Count) : PgCursorDirectionQuantifier
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Count.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Count.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Count.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
