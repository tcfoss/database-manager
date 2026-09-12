using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Update(TableWithJoins Table, SqlValueList<Assignment> Assignments, Expression? Where = null) : Statement
{
    public CommonTableExpression? CommonTableExpression { get; init; }
    public TableWithJoins? From { get; init; }

    /// <summary>
    /// T-SQL <c>OUTPUT</c> clause select items, written between <c>SET</c>
    /// and <c>FROM</c>/<c>WHERE</c>. Null for non-T-SQL dialects.
    /// </summary>
    // TODO: OUTPUT INTO @table | target(cols).
    public SqlValueList<SimpleSelectItem>? Output { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        if (CommonTableExpression != null)
        {
            writer.WriteSql($"{CommonTableExpression} ");
        }

        writer.WriteSql($"UPDATE {Table} SET {Assignments}");
        if (Output != null)
        {
            writer.WriteSql($" OUTPUT {Output}");
        }
        if (From != null)
        {
            writer.WriteSql($" FROM {From}");
        }
        if (Where != null)
        {
            writer.WriteSql($" WHERE {Where}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.UpdateStatement))
        {
            manager.PseudoTables ??= new PseudoTableSet(manager.ActiveSchema, []);
            manager.PseudoTables.EnterSelectScope();

            Table.AddTablesToContext(manager.PseudoTables);
            From?.AddTablesToContext(manager.PseudoTables);

            Meta.FormatPreNonSql(writer, manager);
            CommonTableExpression?.FormatSql(writer, manager);

            writer.WriteSqlI("UPDATE ");
            Table.FormatSql(writer, manager);

            using (manager.Enter(FormatContext.UpdateSetBlock))
            {
                DmlFormatter.FormatAssignmentsClause(writer, manager, Assignments);

                if (Output != null)
                {
                    DmlFormatter.FormatOutputClause(writer, manager, Output);
                }

                if (From != null)
                {
                    DmlFormatter.FormatSingleFromClause(writer, manager, From);
                }

                if (Where != null)
                {
                    DmlFormatter.FormatWhereClause(writer, manager, Where);
                }
            }

            Meta.FormatPostNonSql(writer, manager);
            manager.PseudoTables!.LeaveSelectScope();
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.EnterTableScope())
        {
            using (context.Enter(ReferencedItemsContext.UpdateClause))
            {
                if (context.PseudoTables != null)
                {
                    Table.AddTablesToContext(context.PseudoTables);
                }
                foreach (ItemRef item in Table.GetReferencedItems(context))
                {
                    yield return item;
                }
            }

            if (From != null)
            {
                using (context.Enter(ReferencedItemsContext.FromClause))
                {
                    if (context.PseudoTables != null)
                    {
                        From.AddTablesToContext(context.PseudoTables);
                    }
                    foreach (ItemRef item in From.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }

            foreach (Assignment assignment in Assignments)
            {
                using (context.Enter(ReferencedItemsContext.UpdateValue))
                {
                    foreach (ItemRef item in assignment.Value.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }

            if (Where != null)
            {
                using (context.Enter(ReferencedItemsContext.WhereClause))
                {
                    foreach (ItemRef item in Where.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
