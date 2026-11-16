using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgCursorDirection : IWriteSql //(PgCursorDirectionName Name, Expression? Count, bool All) : IWriteSql
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

    public record Unnamed(PgCursorDirectionQuantifier Quantifier) : PgCursorDirection
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Quantifier.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Quantifier.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Quantifier.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public abstract record BareNamed(string Name) : PgCursorDirection
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Name);
        }
    }

#pragma warning disable CA1716 // Identifiers should not match keywords
    public record Next() : BareNamed("NEXT");
    public record Prior() : BareNamed("PRIOR");
    public record First() : BareNamed("FIRST");
    public record Last() : BareNamed("LAST");

    public abstract record Quantified(string Name, Expression Count) : PgCursorDirection
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Name);
            writer.Write(" ");
            Count.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(Name);
            writer.Write(" ");
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

    public record Absolute(Expression Count) : Quantified("ABSOLUTE", Count);
    public record Relative(Expression Count) : Quantified("RELATIVE", Count);

    public abstract record ExtendedQuantified(string Name, PgCursorDirectionQuantifier? Quantifier) : PgCursorDirection
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Name);
            if (Quantifier is not null)
            {
                writer.Write(" ");
                Quantifier.ToSql(writer);
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(Name);
            if (Quantifier is not null)
            {
                writer.Write(" ");
                Quantifier.FormatSql(writer, manager);
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            if (Quantifier is not null)
            {
                foreach (ItemRef item in Quantifier.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    public record Forward(PgCursorDirectionQuantifier? Quantifier) : ExtendedQuantified("FORWARD", Quantifier);
    public record Backward(PgCursorDirectionQuantifier? Quantifier) : ExtendedQuantified("BACKWARD", Quantifier);

    // public void ToSql(SqlTextWriter writer)
    // {
    //     Name.ToSql(writer);
    //     if (All)
    //     {
    //         writer.Write(" ALL");
    //     }
    //     else if (Count != null)
    //     {
    //         writer.WriteSql($" {Count}");
    //     }
    // }

    // public void FormatSql(SqlTextWriter writer, FormatManager manager)
    // {
    //     Name.ToSql(writer);
    //     if (All)
    //     {
    //         writer.Write(" ALL");
    //     }
    //     else if (Count != null)
    //     {
    //         writer.Write(" ");
    //         Count.FormatSql(writer, manager);
    //     }
    // }

    // public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    // {
    //     if (Count is not null)
    //     {
    //         foreach (ItemRef item in Count.GetReferencedItems(context))
    //         {
    //             yield return item;
    //         }
    //     }
    // }
}
