using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using SqlExpression = TcfOss.DatabaseManager.Core.Expressions.Expression;
using SqlValue = TcfOss.DatabaseManager.Core.Common.Value;

namespace TcfOss.DatabaseManager.Core.Expressions.Components;

public abstract record PatternEscape : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record Character(string Value) : PatternEscape
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($" ESCAPE {new SqlValue.SingleQuotedString(Value)}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return [];
        }
    }

    public record Expression(SqlExpression Value) : PatternEscape
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($" ESCAPE {Value}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(" ESCAPE ");
            Value.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Value.GetReferencedItems(context);
        }
    }
}
