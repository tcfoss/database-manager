using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record PgOpen : Statement
{
    public sealed record Bound(Identifier CursorName, SqlValueList<FunctionArgument>? Arguments) : PgOpen
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OPEN {CursorName}");
            if (Arguments != null)
            {
                writer.Write("(");
                writer.WriteDelimited(Arguments);
                writer.Write(")");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("OPEN ");
            CursorName.FormatSql(writer, manager);
            if (Arguments != null)
            {
                writer.Write("(");
                for (int i = 0; i < Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        writer.Write(", ");
                    }
                    Arguments[i].FormatSql(writer, manager);
                }
                writer.Write(")");
            }
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            if (Arguments != null)
            {
                foreach (FunctionArgument argument in Arguments)
                {
                    foreach (ItemRef item in argument.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    public sealed record ForQuery(
        Identifier CursorName,
        PgCursorScrollMode? ScrollMode,
        Statement Query) : PgOpen
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OPEN {CursorName}");
            if (ScrollMode != null)
            {
                writer.WriteSql($" {ScrollMode}");
            }
            writer.WriteSql($" FOR {Query}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("OPEN ");
            CursorName.FormatSql(writer, manager);
            if (ScrollMode != null)
            {
                writer.Write(" ");
                ScrollMode.ToSql(writer);
            }
            writer.Write(" FOR ");
            Query.FormatSql(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Query.GetReferencedItems(context);
        }
    }

    public sealed record ForExecute(
        Identifier CursorName,
        PgCursorScrollMode? ScrollMode,
        Expression Command,
        SqlValueList<Expression>? Using) : PgOpen
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OPEN {CursorName}");
            if (ScrollMode != null)
            {
                writer.WriteSql($" {ScrollMode}");
            }
            writer.WriteSql($" FOR EXECUTE {Command}");
            if (Using != null)
            {
                writer.WriteSql($" USING {Using}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("OPEN ");
            CursorName.FormatSql(writer, manager);
            if (ScrollMode != null)
            {
                writer.Write(" ");
                ScrollMode.ToSql(writer);
            }
            writer.Write(" FOR EXECUTE ");
            Command.FormatSql(writer, manager);
            if (Using != null)
            {
                writer.Write(" USING ");
                writer.FormatDelimited(Using, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Command.GetReferencedItems(context))
            {
                yield return item;
            }
            if (Using != null)
            {
                foreach (Expression expression in Using)
                {
                    foreach (ItemRef item in expression.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
