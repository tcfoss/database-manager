using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Insert(ObjectName Name, Select? Source) : Statement
{
    public CommonTableExpression? CommonTableExpression { get; init; }
    public bool Into { get; init; }
    public Identifier? Alias { get; init; }
    public SqlValueList<Identifier>? Columns { get; init; }
    public PgIdentityOverride? Overriding { get; init; }
    public SqlValueList<Assignment>? OnDuplicateKeyUpdate { get; init; }
    public PgOnConflict? OnConflict { get; init; }
    public SqlValueList<SimpleSelectItem>? Returning { get; init; }

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
        Name.ToSql(writer);
        if (Alias != null)
        {
            writer.WriteSql($" AS {Alias}");
        }
        writer.Write(" ");
        if (Columns != null)
        {
            writer.WriteSql($"({Columns}) ");
        }
        if (Overriding != null)
        {
            writer.WriteSql($"OVERRIDING {Overriding} ");
        }
        if (Output != null)
        {
            writer.WriteSql($"OUTPUT {Output} ");
        }
        if (Source == null)
        {
            writer.Write("DEFAULT VALUES");
        }
        else
        {
            Source.ToSql(writer);
        }
        if (OnDuplicateKeyUpdate != null)
        {
            writer.WriteSql($" ON DUPLICATE KEY UPDATE {OnDuplicateKeyUpdate}");
        }
        if (OnConflict != null)
        {
            writer.WriteSql($" {OnConflict}");
        }
        if (Returning != null)
        {
            writer.WriteSql($" RETURNING {Returning}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.InsertStatement))
        {
            PseudoTableSet? originalPseudoTables = manager.PseudoTables;
            manager.PseudoTables ??= new PseudoTableSet(manager.ActiveSchema, []);
            manager.PseudoTables.EnterSelectScope();
            new TableFactor.Table(Name) { Alias = Alias }.AddTablesToContext(manager.PseudoTables);

            Meta.FormatPreNonSql(writer, manager);
            CommonTableExpression?.FormatSql(writer, manager);

            DmlFormatter.FormatInsertHead(writer, manager, Name, Into);
            if (Alias != null)
            {
                writer.Write(" AS ");
                writer.WriteSql($"{manager.GetQuotedIdentifier(Alias)}");
            }

            if (Columns != null)
            {
                DmlFormatter.FormatInsertColumns(writer, manager, Columns);
            }
            if (Overriding != null)
            {
                writer.WriteLine();
                writer.WriteSqlI($"OVERRIDING {Overriding}");
            }

            if (Output != null)
            {
                DmlFormatter.FormatOutputClause(writer, manager, Output);
            }

            writer.WriteLine();

            using (manager.Enter(FormatContext.InsertValues))
            {
                if (Source == null)
                {
                    writer.WriteSqlI("DEFAULT VALUES");
                }
                else
                {
                    Source.FormatSql(writer, manager);
                }
            }

            if (OnDuplicateKeyUpdate != null)
            {
                DmlFormatter.FormatInsertOnDuplicateKeyUpdate(writer, manager, OnDuplicateKeyUpdate);
            }
            if (OnConflict != null)
            {
                writer.WriteLine();
                writer.Write(manager.Indent);
                OnConflict.FormatSql(writer, manager);
            }
            if (Returning != null)
            {
                DmlFormatter.FormatReturningClause(writer, manager, Returning);
            }
            Meta.FormatPostNonSql(writer, manager);
            manager.PseudoTables.LeaveSelectScope();
            manager.PseudoTables = originalPseudoTables;
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return CommonTableExpression?.GetReferencedItems(context, GetBodyReferencedItems(context)) ?? GetBodyReferencedItems(context);
    }

    private IEnumerable<ItemRef> GetBodyReferencedItems(ReferencedItemsManager context)
    {
        using (context.EnterTableScope())
        {
            if (context.PseudoTables != null)
            {
                new TableFactor.Table(Name) { Alias = Alias }.AddTablesToContext(context.PseudoTables);
            }
            using (context.Enter(ReferencedItemsContext.InsertTarget))
            {
                ItemRef? item = context.CreateObjectRef(Name);
                if (item != null)
                {
                    yield return item;
                }
            }
            foreach (ItemRef item in Source?.GetReferencedItems(context) ?? [])
            {
                yield return item;
            }
            using (context.Enter(ReferencedItemsContext.InsertValueList))
            {
                foreach (ItemRef item in OnConflict?.GetReferencedItems(context) ?? [])
                {
                    yield return item;
                }
            }
            using (context.Enter(ReferencedItemsContext.SelectItem))
            {
                foreach (SimpleSelectItem selection in Returning ?? [])
                {
                    foreach (ItemRef item in selection.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
