using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public abstract record CompositeAccess(Expression Expression) : Expression
{
    public sealed record Field(Expression Expression, Identifier Name) : CompositeAccess(Expression)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Expression}.{Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Expression.FormatSql(writer, manager);
            writer.Write(".");
            Name.FormatSql(writer, manager);
        }
    }

    public sealed record Wildcard(Expression Expression) : CompositeAccess(Expression)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Expression}.*");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Expression.FormatSql(writer, manager);
            writer.Write(".*");
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Expression.GetReferencedItems(context);
    }
}
