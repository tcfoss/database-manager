using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record SimpleSelectItem() : IWriteSql, IHaveMeta
{
    public MetaData Meta { get; set; } = new();

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record UnnamedExpression(Expression Expression) : SimpleSelectItem
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Expression.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            Expression.FormatSql(writer, manager);
            if (!manager.SelectItemsLast)
            {
                writer.WriteLine(",");
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Expression.GetReferencedItems(context);
        }
    }

    public record QualifiedWildcard(ObjectName Name) : SimpleSelectItem
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}.*");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            SqlValueList<Identifier> identifiers = manager.GetPseudoTable(Name.Values.Count > 1, Name.Values) ?? Name.Values;
            identifiers = manager.QuoteIdentifiers(Name.Values, identifiers);

            writer.Write(manager.Indent);
            writer.WriteDelimited(identifiers, ".");
            writer.Write(".*");
            if (!manager.SelectItemsLast)
            {
                writer.WriteLine(",");
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            ItemRef? item = context.CreateObjectRef(Name);
            if (item != null)
            {
                yield return item;
            }
        }
    }

    public record ExpressionWithAlias(Expression Expression, Identifier Alias) : SimpleSelectItem
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Expression} AS {Alias}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write($"{manager.Indent}");
            Expression.FormatSql(writer, manager);
            writer.WriteSql($" AS {Alias}");
            if (!manager.SelectItemsLast)
            {
                writer.WriteLine(",");
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Expression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public record Wildcard() : SimpleSelectItem
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("*");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write($"{manager.Indent}*");
            if (!manager.SelectItemsLast)
            {
                writer.WriteLine(",");
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }

    // public record SimpleSelectExpression(SimpleSelect Select) : SimpleSelectItem
    // {
    //     public override void ToSql(SqlTextWriter writer)
    //     {
    //         Select.ToSql(writer);
    //     }
    // }
}
