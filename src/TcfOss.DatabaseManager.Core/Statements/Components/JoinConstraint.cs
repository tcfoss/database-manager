using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record JoinConstraint()
{
    public record On(Expression Expression) : JoinConstraint
    {
        public void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            using (manager.Enter(FormatContext.JoinCondition))
            {
                if (manager.Formatting.JoinConditionIndent.HasValue)
                {
                    writer.WriteLine();
                    manager.IncreaseIndent(manager.Formatting.JoinConditionIndent);
                    writer.WriteSql($"{manager.Indent}ON ");
                    Expression.FormatSql(writer, manager);
                    manager.DecreaseIndent(manager.Formatting.JoinConditionIndent);
                }
                else
                {
                    writer.Write(" ON ");
                    Expression.FormatSql(writer, manager);
                }
            }
        }
    }

    public record Using(SqlValueList<Identifier> Identifiers) : JoinConstraint
    {
        public void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            using (manager.Enter(FormatContext.JoinCondition))
            {
                if (manager.Formatting.JoinConditionIndent.HasValue)
                {
                    writer.WriteLine();
                    manager.IncreaseIndent(manager.Formatting.JoinConditionIndent);
                    writer.Write($"{manager.Indent}USING (");
                    writer.FormatDelimited(Identifiers, manager);
                    writer.Write(")");
                    manager.DecreaseIndent(manager.Formatting.JoinConditionIndent);
                }
                else
                {
                    writer.Write(" USING (");
                    writer.FormatDelimited(Identifiers, manager);
                    writer.Write(")");
                }
            }
        }
    }

    public record Natural() : JoinConstraint;

    public record None() : JoinConstraint;
}
