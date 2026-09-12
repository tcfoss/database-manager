using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Delete(SqlValueList<TableWithJoins> From, Expression? Where) : Statement
{
    public CommonTableExpression? CommonTableExpression { get; init; }
    public SqlValueList<ObjectName>? DeleteTables { get; init; }
    public SqlValueList<OrderBy>? OrderBy { get; init; }
    public Expression? Limit { get; init; }
    public SqlValueList<SimpleSelectItem>? Returning { get; init; }

    /// <summary>
    /// T-SQL <c>OUTPUT</c> clause select items, written between <c>FROM</c>
    /// and <c>WHERE</c>. Null for non-T-SQL dialects.
    /// </summary>
    // TODO: OUTPUT INTO @table | target(cols).
    public SqlValueList<SimpleSelectItem>? Output { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        if (CommonTableExpression != null)
        {
            writer.WriteSql($"{CommonTableExpression} ");
        }

        writer.Write("DELETE");
        if (DeleteTables.SafeAny())
        {
            writer.WriteSql($" {DeleteTables}");
        }
        writer.WriteSql($" FROM {From}");

        if (Output != null)
        {
            writer.WriteSql($" OUTPUT {Output}");
        }

        if (Where != null)
        {
            writer.WriteSql($" WHERE {Where}");
        }

        if (OrderBy.SafeAny())
        {
            writer.WriteSql($" ORDER BY {OrderBy}");
        }

        if (Limit != null)
        {
            writer.WriteSql($" LIMIT {Limit}");
        }

        if (Returning.SafeAny())
        {
            writer.WriteSql($" RETURNING {Returning}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.DeleteStatement))
        {
            manager.PseudoTables ??= new PseudoTableSet(manager.ActiveSchema, []);
            manager.PseudoTables.EnterSelectScope();

            foreach (TableWithJoins from in From)
            {
                from.AddTablesToContext(manager.PseudoTables);
            }

            Meta.FormatPreNonSql(writer, manager);
            CommonTableExpression?.FormatSql(writer, manager);

            writer.WriteSqlI("DELETE");

            if (DeleteTables.SafeAny())
            {
                DmlFormatter.FormatDeleteTableList(writer, manager, DeleteTables!);
            }

            DmlFormatter.FormatFromClause(writer, manager, From);

            if (Output != null)
            {
                DmlFormatter.FormatOutputClause(writer, manager, Output);
            }

            if (Where != null)
            {
                DmlFormatter.FormatWhereClause(writer, manager, Where);
            }

            if (OrderBy.SafeAny())
            {
                DmlFormatter.FormatOrderByClause(writer, manager, OrderBy!);
            }

            if (Limit != null)
            {
                writer.WriteLine();
                writer.WriteSqlI("LIMIT ");
                Limit.FormatSql(writer, manager);
            }

            if (Returning.SafeAny())
            {
                DmlFormatter.FormatReturningClause(writer, manager, Returning!);
            }

            Meta.FormatPostNonSql(writer, manager);
            manager.PseudoTables!.LeaveSelectScope();
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.EnterTableScope())
        {
            using (context.Enter(ReferencedItemsContext.FromClause))
            {
                foreach (TableWithJoins table in From)
                {
                    if (context.PseudoTables != null)
                    {
                        table.AddTablesToContext(context.PseudoTables);
                    }
                    foreach (ItemRef item in table.GetReferencedItems(context))
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
