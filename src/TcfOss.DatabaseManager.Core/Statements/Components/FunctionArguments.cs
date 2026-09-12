using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record FunctionArguments() : IWriteSql
{
    // TODO: Move to Expressions/Components
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record None() : FunctionArguments
    {
        public override void ToSql(SqlTextWriter writer)
        {
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }

    // public record Subquery(SimpleSelect Query) : FunctionArguments
    // {
    //     public override void ToSql(SqlTextWriter writer)
    //     {
    //         writer.WriteSql($"({Query})");
    //     }
    // }

    public record List(SqlValueList<FunctionArgument>? Arguments = null, DuplicateTreatment? DuplicateTreatment = null, SqlValueList<FunctionArgumentClause>? Clauses = null) : FunctionArguments
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("(");

            if (DuplicateTreatment != null)
            {
                writer.WriteSql($"{DuplicateTreatment} ");
            }

            writer.WriteDelimited(Arguments);

            if (Clauses.SafeAny())
            {
                writer.WriteSql($" {Clauses.ToSqlDelimited(" ")}");
            }
            writer.Write(")");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("(");

            if (DuplicateTreatment != null)
            {
                writer.WriteSql($"{DuplicateTreatment} ");
            }

            if (Arguments != null)
            {
                for (int i = 0; i < Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        writer.Write(", ");
                    }
                    Arguments[i].FormatSql(writer, manager);
                }
            }

            if (Clauses.SafeAny())
            {
                writer.WriteSql($" {Clauses.ToSqlDelimited(" ")}");
            }
            writer.Write(")");
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

            if (Clauses != null)
            {
                foreach (FunctionArgumentClause clause in Clauses)
                {
                    foreach (ItemRef item in clause.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    public static List EmptyList()
    {
        return new List();
    }
}
