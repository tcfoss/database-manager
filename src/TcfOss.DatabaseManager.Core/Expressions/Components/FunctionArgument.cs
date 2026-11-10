using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using FunctionArgumentOperator = TcfOss.DatabaseManager.Core.Expressions.Attributes.FunctionArgumentOperator;

namespace TcfOss.DatabaseManager.Core.Expressions.Components;

public abstract record FunctionArgument() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record Named(Identifier Name, FunctionArgumentExpression Argument, FunctionArgumentOperator Operator) : FunctionArgument
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} {Operator} {Argument}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
            writer.WriteSql($" {Operator} ");
            Argument.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Argument.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public record Variadic(FunctionArgument Argument) : FunctionArgument
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"VARIADIC {Argument}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("VARIADIC ");
            Argument.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Argument.GetReferencedItems(context);
        }
    }

    public record Unnamed(FunctionArgumentExpression Argument) : FunctionArgument
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Argument.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Argument.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Argument.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
