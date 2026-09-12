using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Formatting;

internal static class DmlFormatter
{
    /// <summary>
    /// Formats the selection list of a SELECT statement, expanding wildcards
    /// into explicit column references when <see cref="Configuration.FormattingSettings.ExpandWildcards"/>
    /// is enabled and pseudo-table information is available.
    /// Must be called at the correct indent level; does not change indent.
    /// </summary>
    public static void FormatSelections(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<SimpleSelectItem> selections)
    {
        List<(SimpleSelectItem? Original, string? Source, string? Column)> items = GetExpandedSelectItems(manager, selections);

        for (int i = 0; i < items.Count; i++)
        {
            (SimpleSelectItem? original, string? source, string? column) = items[i];
            bool isLast = i == items.Count - 1;
            if (original != null)
            {
                manager.SelectItemsFirst = i == 0;
                manager.SelectItemsLast = isLast;
                original.FormatSql(writer, manager);
            }
            else
            {
                writer.Write(manager.Indent);
                new Identifier(source!).FormatSql(writer, manager);
                writer.Write(".");
                new Identifier(column!).FormatSql(writer, manager);
                if (!isLast)
                {
                    writer.WriteLine(",");
                }
            }
        }
    }

    private static List<(SimpleSelectItem? Original, string? Source, string? Column)> GetExpandedSelectItems(FormatManager manager, SqlValueList<SimpleSelectItem> selections)
    {
        bool expandWildcards = manager.Formatting.ExpandWildcards && manager.PseudoTables != null;
        List<(SimpleSelectItem? Original, string? Source, string? Column)> items = [];
        foreach (SimpleSelectItem item in selections)
        {
            if (expandWildcards && item is SimpleSelectItem.Wildcard)
            {
                IReadOnlyList<PseudoTableSet.SelectableItem> cols = manager.PseudoTables?.GetLocalWildcardColumns() ?? [];
                if (cols.Count > 0)
                {
                    foreach (PseudoTableSet.SelectableItem col in cols)
                    {
                        items.Add((null, col.Source, col.Name));
                    }
                    continue;
                }
            }
            else if (expandWildcards && item is SimpleSelectItem.QualifiedWildcard qw)
            {
                IReadOnlyList<PseudoTableSet.SelectableItem> cols = manager.PseudoTables?.GetLocalWildcardColumns(qw.Name.Values.Last().Name) ?? [];
                if (cols.Count > 0)
                {
                    foreach (PseudoTableSet.SelectableItem col in cols)
                    {
                        items.Add((null, col.Source, col.Name));
                    }
                    continue;
                }
            }
            items.Add((item, null, null));
        }
        return items;
    }

    /// <summary>
    /// Formats a <c>FROM</c> clause containing one or more <see cref="TableWithJoins"/>.
    /// Writes a newline before <c>FROM</c>.
    /// </summary>
    public static void FormatFromClause(
        SqlTextWriter writer,
        FormatManager manager,
        IEnumerable<TableWithJoins> from)
    {
        using (manager.Enter(FormatContext.FromClause))
        {
            writer.WriteLine();
            writer.WriteSqlI("FROM ");
            foreach (TableWithJoins table in from)
            {
                table.FormatSql(writer, manager);
                writer.WriteLine(",");
            }
            writer.RemoveChar(',');
        }
    }

    /// <summary>
    /// Formats a single-table <c>FROM</c> clause. Writes a newline before <c>FROM</c>.
    /// </summary>
    public static void FormatSingleFromClause(
        SqlTextWriter writer,
        FormatManager manager,
        TableWithJoins from)
    {
        writer.WriteLine();
        writer.WriteSqlI("FROM ");
        from.FormatSql(writer, manager);
    }

    /// <summary>
    /// Formats a <c>WHERE</c> clause. Writes a newline before <c>WHERE</c>.
    /// </summary>
    public static void FormatWhereClause(
        SqlTextWriter writer,
        FormatManager manager,
        Expression where)
    {
        using (manager.Enter(FormatContext.WhereClause))
        {
            writer.WriteLine();
            writer.WriteSqlI("WHERE ");
            where.FormatSql(writer, manager);
        }
    }

    /// <summary>
    /// Formats an <c>ORDER BY</c> clause. Writes a newline before <c>ORDER BY</c>.
    /// </summary>
    public static void FormatOrderByClause(
        SqlTextWriter writer,
        FormatManager manager,
        IEnumerable<OrderBy> orderBy)
    {
        writer.WriteLine();
        writer.WriteSqlI("ORDER BY ");
        foreach (OrderBy item in orderBy)
        {
            item.FormatSql(writer, manager);
            writer.Write(", ");
        }
        writer.RemoveChar(',');
    }

    /// <summary>
    /// Formats a <c>GROUP BY</c> clause. Writes a newline before <c>GROUP BY</c>.
    /// </summary>
    public static void FormatGroupByClause(
        SqlTextWriter writer,
        FormatManager manager,
        IEnumerable<Expression> groupBy)
    {
        using (manager.Enter(FormatContext.GroupByClause))
        {
            writer.WriteLine();
            writer.WriteSqlI("GROUP BY ");
            writer.FormatDelimited(groupBy, manager);
        }
    }

    /// <summary>
    /// Formats a <c>HAVING</c> clause. Writes a newline before <c>HAVING</c>.
    /// </summary>
    public static void FormatHavingClause(
        SqlTextWriter writer,
        FormatManager manager,
        Expression having)
    {
        using (manager.Enter(FormatContext.HavingClause))
        {
            writer.WriteLine();
            writer.WriteSqlI("HAVING ");
            having.FormatSql(writer, manager);
        }
    }

    /// <summary>
    /// Formats a <c>SELECT INTO</c> clause. Writes a newline before the clause.
    /// </summary>
    public static void FormatSelectIntoClause(
        SqlTextWriter writer,
        FormatManager manager,
        SelectInto selectInto)
    {
        writer.WriteLine();
        selectInto.FormatSql(writer, manager);
    }

    /// <summary>
    /// Formats a <c>RETURNING</c> clause including its select items.
    /// Writes a newline before <c>RETURNING</c>.
    /// </summary>
    public static void FormatReturningClause(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<SimpleSelectItem> returning)
    {
        writer.WriteLine();
        writer.WriteSqlI("RETURNING");
        writer.WriteLine();
        manager.IncreaseIndent();
        for (int i = 0; i < returning.Count; i++)
        {
            manager.SelectItemsFirst = i == 0;
            manager.SelectItemsLast = i == returning.Count - 1;
            returning[i].FormatSql(writer, manager);
        }
        manager.DecreaseIndent();
    }

    /// <summary>
    /// Formats a T-SQL <c>OUTPUT</c> clause including its select items.
    /// Writes a newline before <c>OUTPUT</c>.
    /// </summary>
    public static void FormatOutputClause(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<SimpleSelectItem> output)
    {
        writer.WriteLine();
        writer.WriteSqlI("OUTPUT");
        writer.WriteLine();
        manager.IncreaseIndent();
        for (int i = 0; i < output.Count; i++)
        {
            manager.SelectItemsFirst = i == 0;
            manager.SelectItemsLast = i == output.Count - 1;
            output[i].FormatSql(writer, manager);
        }
        manager.DecreaseIndent();
    }

    /// <summary>
    /// Formats the optional target-table list that follows <c>DELETE</c> in
    /// multi-table delete syntax (e.g. <c>DELETE t1, t2 FROM ...</c>).
    /// </summary>
    public static void FormatDeleteTableList(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<ObjectName> deleteTables)
    {
        writer.Write(" ");
        for (int i = 0; i < deleteTables.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            ObjectName name = deleteTables[i];
            SqlValueList<Identifier>? resolved = manager.GetPseudoTable(manager.Formatting.ObjectNamePrefixWithSchema, name.Values);
            Identifier[] quoted = manager.QuoteIdentifiers(name.Values, resolved ?? name.Values);
            writer.WriteDelimited(quoted, ".");
        }
    }

    /// <summary>
    /// Formats the <c>SET</c> block of an <c>UPDATE</c> statement.
    /// Writes a newline before <c>SET</c>.
    /// </summary>
    public static void FormatAssignmentsClause(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<Assignment> assignments)
    {
        writer.WriteLine();
        writer.WriteSqlI("SET");
        writer.WriteLine();
        manager.IncreaseIndent();
        for (int i = 0; i < assignments.Count; i++)
        {
            writer.Write(manager.Indent);
            assignments[i].FormatSql(writer, manager);
            if (i < assignments.Count - 1)
            {
                writer.WriteLine(",");
            }
        }
        manager.DecreaseIndent();
    }

    /// <summary>
    /// Formats the <c>INSERT [INTO] table_name</c> head of an INSERT statement,
    /// including schema-prefix resolution.
    /// </summary>
    public static void FormatInsertHead(
        SqlTextWriter writer,
        FormatManager manager,
        ObjectName name,
        bool into)
    {
        writer.WriteSqlI("INSERT ");
        if (into)
        {
            writer.Write("INTO ");
        }
        SqlValueList<Identifier> nameIdentifiers = manager.GetPseudoTable(manager.Formatting.ObjectNamePrefixWithSchema, name.Values) ?? name.Values;
        Identifier[] quotedName = manager.QuoteIdentifiers(name.Values, nameIdentifiers);
        writer.WriteDelimited(quotedName, ".");
    }

    /// <summary>
    /// Formats the column list of an INSERT statement, including parentheses.
    /// Writes a newline before the opening parenthesis.
    /// </summary>
    public static void FormatInsertColumns(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<Identifier> columns)
    {
        using (manager.Enter(FormatContext.InsertColumns))
        {
            if (manager.Formatting.OpeningParensOnNewLine)
            {
                writer.WriteLine();
                writer.WriteLine($"{manager.Indent}(");
            }
            else
            {
                writer.WriteLine(" (");
            }

            manager.IncreaseIndent();
            for (int i = 0; i < columns.Count; i++)
            {
                writer.Write(manager.Indent);
                columns[i].FormatSql(writer, manager);
                if (i < columns.Count - 1)
                {
                    writer.WriteLine(",");
                }
            }
            manager.DecreaseIndent();
            writer.WriteLine();
            writer.Write($"{manager.Indent})");
        }
    }

    public static void FormatInsertOnDuplicateKeyUpdate(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<Assignment> assignments)
    {
        using (manager.Enter(FormatContext.InsertOnDuplicateUpdate))
        {
            writer.WriteLine();
            writer.WriteSqlI("ON DUPLICATE KEY UPDATE");
            writer.WriteLine();
            manager.IncreaseIndent();
            for (int i = 0; i < assignments.Count; i++)
            {
                writer.Write(manager.Indent);
                assignments[i].FormatSql(writer, manager);
                if (i < assignments.Count - 1)
                {
                    writer.WriteLine(",");
                }
            }
            manager.DecreaseIndent();
        }
    }

    public static string ConstructValuesRowStart(FormatManager manager, bool rowConstructor, bool multiline)
    {
        if (rowConstructor)
        {
            if (multiline && manager.Formatting.OpeningParensOnNewLine)
            {
                return $"{manager.Indent}ROW\n{manager.Indent}(\n";
            }
            if (multiline)
            {
                return $"{manager.Indent}ROW (\n";
            }
            return $"{manager.Indent}ROW (";
        }
        if (multiline)
        {
            return $"{manager.Indent}(\n";
        }
        return $"{manager.Indent}(";
    }
}
