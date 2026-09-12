using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Insert(ObjectName Name, Select Source) : Statement
{
    public CommonTableExpression? CommonTableExpression { get; init; }
    public bool Into { get; init; }
    public SqlValueList<Identifier>? Columns { get; init; }
    public SqlValueList<Assignment>? OnDuplicateKeyUpdate { get; init; }

    /// <summary>
    /// T-SQL <c>OUTPUT</c> clause select items, written between the column
    /// list and <c>VALUES</c>/source. Null for non-T-SQL dialects.
    /// </summary>
    // TODO: OUTPUT INTO @table | target(cols).
    public SqlValueList<SimpleSelectItem>? Output { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        if (CommonTableExpression != null)
        {
            writer.WriteSql($"{CommonTableExpression} ");
        }

        writer.Write("INSERT ");
        if (Into)
        {
            writer.Write("INTO ");
        }
        writer.WriteSql($"{Name} ");
        if (Columns != null)
        {
            writer.WriteSql($"({Columns}) ");
        }
        if (Output != null)
        {
            writer.WriteSql($"OUTPUT {Output} ");
        }
        Source.ToSql(writer);
        if (OnDuplicateKeyUpdate != null)
        {
            writer.WriteSql($" ON DUPLICATE KEY UPDATE {OnDuplicateKeyUpdate}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.InsertStatement))
        {
            Meta.FormatPreNonSql(writer, manager);
            CommonTableExpression?.FormatSql(writer, manager);

            DmlFormatter.FormatInsertHead(writer, manager, Name, Into);

            if (Columns != null)
            {
                DmlFormatter.FormatInsertColumns(writer, manager, Columns);
            }

            if (Output != null)
            {
                DmlFormatter.FormatOutputClause(writer, manager, Output);
            }

            writer.WriteLine();

            using (manager.Enter(FormatContext.InsertValues))
            {
                Source.FormatSql(writer, manager);
            }

            if (OnDuplicateKeyUpdate != null)
            {
                PseudoTableSet? originalPseudoTables = manager.PseudoTables;

                manager.PseudoTables ??= new PseudoTableSet(manager.ActiveSchema, []);
                manager.PseudoTables.EnterSelectScope();

                // Register the insert target so that ON DUPLICATE KEY UPDATE column references
                // can be resolved against (and optionally qualified with) the target table.
                PseudoTable[] targetCandidates = manager.PseudoTables.GetPseudoTables(Name);
                if (targetCandidates.Length == 1)
                {
                    PseudoTable target = targetCandidates[0];
                    manager.PseudoTables.AddLocalSource(target.Name, target);
                }
                DmlFormatter.FormatInsertOnDuplicateKeyUpdate(writer, manager, OnDuplicateKeyUpdate);

                manager.PseudoTables.LeaveSelectScope();
                manager.PseudoTables = originalPseudoTables;
            }
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.InsertTarget))
        {
            ItemRef? item = context.CreateObjectRef(Name);
            if (item != null)
            {
                yield return item;
            }
        }

        foreach (ItemRef item in Source.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
