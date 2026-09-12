using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Formatting;

internal static class RoutineFormatter
{
    /// <summary>
    /// Writes <c>CREATE [createOrLabel]</c> then, indented one level, an optional
    /// <c>DEFINER = ...</c> line. Does not write any trailing newline.
    /// </summary>
    public static void FormatCreateAndDefiner(
        SqlTextWriter writer,
        FormatManager manager,
        CreateOrLabel? createOrLabel,
        Definer? definer)
    {
        writer.WriteSqlI("CREATE");
        if (createOrLabel != null)
        {
            writer.WriteSql($" {createOrLabel}");
        }

        if (definer != null)
        {
            writer.WriteLine();
            manager.IncreaseIndent();
            writer.WriteSqlI($"{definer}");
            manager.DecreaseIndent();
        }
    }

    /// <summary>
    /// Formats the header common to both CREATE FUNCTION and CREATE PROCEDURE:
    /// <code>
    /// CREATE [CreateOrLabel]
    ///     [Definer]
    /// routine_keyword routine_name (parameters)
    ///     [IF NOT EXISTS]
    ///     [characteristic]
    /// </code>
    /// The indents shown are relative to the overall statement indent. If
    /// the number of parameters is greater than or equal to
    /// <see cref="Configuration.FormattingSettings.RoutineParameterMultiLineThreshold"/>,
    /// parameters are placed one per line, indented from the routine name line.
    /// </summary>
    public static void FormatHeader(
        SqlTextWriter writer,
        FormatManager manager,
        CreateOrLabel? createOrLabel,
        Definer? definer,
        string routineKeyword,
        ObjectName name,
        SqlValueList<RoutineParameter> parameters,
        bool ifNotExists,
        DataType? returnType,
        MyRoutineCharacteristic? characteristic,
        MsRoutineWithOptions? msOptions)
    {
        // CREATE [OR REPLACE / OR ALTER] [Definer]
        FormatCreateAndDefiner(writer, manager, createOrLabel, definer);

        // routine_keyword routine_name
        writer.WriteLine();
        writer.WriteSqlI($"{routineKeyword} ");
        name.FormatSql(writer, manager);

        // (parameters)
        int paramCount = parameters.Count;
        bool multiLine = manager.Formatting.RoutineParameterMultiLineThreshold.HasValue
                         && paramCount >= manager.Formatting.RoutineParameterMultiLineThreshold.Value
                         && paramCount > 0;

        if (multiLine)
        {
            writer.Write(" (");
            writer.WriteLine();
            manager.IncreaseIndent();
            for (int i = 0; i < paramCount; i++)
            {
                writer.Write(manager.Indent);
                parameters[i].FormatSql(writer, manager);
                if (i < paramCount - 1)
                {
                    writer.WriteLine(",");
                }
            }
            writer.WriteLine();
            manager.DecreaseIndent();
            writer.Write(manager.Indent);
            writer.Write(")");
        }
        else
        {
            writer.Write(" (");
            for (int i = 0; i < paramCount; i++)
            {
                if (i > 0)
                {
                    writer.Write(", ");
                }
                parameters[i].FormatSql(writer, manager);
            }
            writer.Write(")");
        }

        // [IF NOT EXISTS] — indented
        if (ifNotExists)
        {
            writer.WriteLine();
            manager.IncreaseIndent();
            writer.WriteSqlI("IF NOT EXISTS");
            manager.DecreaseIndent();
        }

        if (returnType != null)
        {
            writer.WriteLine();
            writer.WriteSqlI($"RETURNS {returnType}");
        }

        // [characteristic] — indented
        if (characteristic != null)
        {
            writer.WriteLine();
            manager.IncreaseIndent();
            writer.WriteSqlI($"{characteristic}");
            manager.DecreaseIndent();
        }

        // [WITH options] — indented
        if (msOptions != null)
        {
            writer.WriteLine();
            manager.IncreaseIndent();
            msOptions.FormatSql(writer, manager);
            manager.DecreaseIndent();
        }
    }

    public static void FormatBody(SqlTextWriter writer, FormatManager manager, Statement body, bool useAsBeforeBody = false)
    {
        writer.WriteLine();
        if (useAsBeforeBody)
        {
            writer.WriteSqlI("AS");
            writer.WriteLine();
        }
        if (body is BeginEnd)
        {
            body.FormatSql(writer, manager);
        }
        else
        {
            manager.IncreaseIndent();
            body.FormatSql(writer, manager);
            manager.DecreaseIndent();
        }
    }
}
