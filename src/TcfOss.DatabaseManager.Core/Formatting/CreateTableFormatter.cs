using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Formatting;

internal static class CreateTableFormatter
{
    /// <summary>
    /// Formats the body of a <c>CREATE TABLE</c> statement: the column definitions
    /// and table constraints inside the parentheses.
    /// Writes a newline before the opening parenthesis.
    /// </summary>
    public static void FormatBody(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<StatementColumn>? columns,
        SqlValueList<StatementTableConstraint> constraints)
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

        if (columns.SafeAny())
        {
            writer.FormatDelimitedLines(columns, manager, delimiter: ",");
            if (constraints.Any())
            {
                writer.WriteLine(",");
            }
        }

        if (constraints.Any())
        {
            writer.FormatDelimitedLines(constraints, manager, delimiter: ",");
        }

        writer.WriteLine("");
        manager.DecreaseIndent();
        writer.Write($"{manager.Indent})");
    }

    /// <summary>
    /// Formats the table options that follow the column/constraint list
    /// (e.g. <c>ENGINE=InnoDB CHARACTER SET utf8mb4</c>).
    /// Writes a leading space before the options.
    /// </summary>
    public static void FormatTableOptions(
        SqlTextWriter writer,
        FormatManager manager,
        SqlValueList<StatementTableOption> tableOptions)
    {
        writer.Write(" ");
        writer.FormatDelimited(tableOptions, manager, " ");
    }

    /// <summary>
    /// Formats the <c>[AS] SELECT ...</c> tail of a <c>CREATE TABLE ... AS SELECT</c>.
    /// Writes a newline before the <c>AS</c> keyword (when present) and before the SELECT.
    /// </summary>
    public static void FormatAsSelect(
        SqlTextWriter writer,
        FormatManager manager,
        Select asSelect,
        bool? printAs)
    {
        if (printAs ?? false)
        {
            writer.WriteLine();
            writer.WriteSql($"{manager.Indent}AS");
        }
        writer.WriteLine();
        asSelect.FormatSql(writer, manager);
    }
}
