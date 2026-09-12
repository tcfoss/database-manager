using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record Values(SqlValueList<SqlValueList<Expression>> Rows) : IWriteSql
{
    public bool RowConstructor { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write("VALUES ");
        string prefix = RowConstructor ? "ROW " : "";
        for (int i = 0; i < Rows.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            writer.WriteSql($"{prefix}({Rows[i]})");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSqlI("VALUES");
        writer.WriteLine();
        bool multiline = manager.Formatting.ValueListMultiLineThreshold.HasValue
            && Rows.Count > 0
            && Rows[0].Count >= manager.Formatting.ValueListMultiLineThreshold.Value;

        string rowStart = DmlFormatter.ConstructValuesRowStart(manager, RowConstructor, multiline);

        for (int iRow = 0; iRow < Rows.Count; iRow++)
        {
            writer.Write(rowStart);

            if (multiline)
            {
                manager.IncreaseIndent();
            }

            SqlValueList<Expression> row = Rows[iRow];
            for (int iCol = 0; iCol < row.Count; iCol++)
            {
                if (multiline)
                {
                    writer.Write(manager.Indent);
                }

                row[iCol].FormatSql(writer, manager);

                if (iCol < row.Count - 1)
                {
                    writer.Write(",");
                    if (multiline)
                    {
                        writer.WriteLine();
                    }
                    else
                    {
                        writer.Write(" ");
                    }
                }
            }

            if (multiline)
            {
                writer.WriteLine();
                manager.DecreaseIndent();
                writer.Write($"{manager.Indent})");
            }
            else
            {
                writer.Write(")");
            }

            if (iRow < Rows.Count - 1)
            {
                writer.WriteLine(",");
            }
        }
    }
}
