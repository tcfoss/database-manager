using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record Join(TableFactor? Relation, JoinOperator? JoinOperator) : IWriteSql, IAddTablesToContext
{
    public void ToSql(SqlTextWriter writer)
    {
        if (JoinOperator is JoinOperator.ConstrainedJoinOperator op)
        {
            if (op.JoinConstraint is JoinConstraint.Natural)
            {
                writer.Write(" NATURAL");
            }
            writer.WriteSql($" {op.JoinText} {Relation}");
            if (op.JoinConstraint is JoinConstraint.On joinOn)
            {
                writer.WriteSql($" ON {joinOn.Expression}");
            }
            else if (op.JoinConstraint is JoinConstraint.Using joinUsing)
            {
                writer.WriteSql($" USING ({joinUsing.Identifiers})");
            }
        }
        else if (JoinOperator != null)
        {
            writer.WriteSql($" {JoinOperator.JoinText} {Relation}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.Join))
        {
            if (JoinOperator is JoinOperator.ConstrainedJoinOperator op)
            {
                writer.Write(manager.Indent);
                if (op.JoinConstraint is JoinConstraint.Natural)
                {
                    writer.Write("NATURAL ");
                }
                writer.Write(op.JoinText);
                if (Relation != null)
                {
                    writer.Write(" ");
                }
                Relation?.FormatSql(writer, manager);

                if (op.JoinConstraint is JoinConstraint.On joinOn)
                {
                    joinOn.FormatSql(writer, manager);
                }
                else if (op.JoinConstraint is JoinConstraint.Using joinUsing)
                {
                    joinUsing.FormatSql(writer, manager);
                }
            }
            else if (JoinOperator != null)
            {
                writer.WriteSqlI($"{JoinOperator.JoinText}");
                writer.Write(" ");
                Relation?.FormatSql(writer, manager);
            }
        }
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Relation != null)
        {
            foreach (ItemRef item in Relation.GetReferencedItems(context))
            {
                yield return item;
            }
        }

        if (JoinOperator is JoinOperator.ConstrainedJoinOperator { JoinConstraint: JoinConstraint.On joinOn })
        {
            using (context.Enter(ReferencedItemsContext.JoinCondition))
            {
                foreach (ItemRef item in joinOn.Expression.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    /// <inheritdoc/>
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        Relation?.AddTablesToContext(pseudoTableSet, sourceRef);
    }
}
