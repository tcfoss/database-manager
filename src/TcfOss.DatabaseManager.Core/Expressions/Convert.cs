using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public abstract record Convert(Expression Expression) : Expression
{

    public record UsingCharset(Expression Expression, string CharacterSet) : Convert(Expression)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CONVERT({Expression} USING {CharacterSet})");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Expression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public record ToDataType(Expression Expression, DataType DataType) : Convert(Expression)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CONVERT({Expression}, {DataType})");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Expression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
