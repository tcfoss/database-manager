using System.Runtime.CompilerServices;

namespace TcfOss.DatabaseManager.Core.IO;

[InterpolatedStringHandler]
public readonly ref struct IndentedSqlInterpolatedStringHandler
{
    private readonly SqlTextWriter _writer;

    public IndentedSqlInterpolatedStringHandler(int literalLength, int formattedCount, SqlTextWriter writer)
    {
        _writer = writer;
        _writer.Write(_writer.Indenter.IndentString);
    }

    public void AppendLiteral(string? value)
    {
        _writer.Write(value);
    }

    public void AppendFormatted<T>(T value)
    {
        switch (value)
        {
            case IWriteSql sql:
                sql.ToSql(_writer);
                break;

            case Enum e:
                AppendLiteral(e.ToString());
                break;

            case string str:
                if (!string.IsNullOrEmpty(str))
                {
                    AppendLiteral(str);
                }
                break;

            default:
                if (value != null)
                {
                    AppendFormatted(value.ToString());
                }
                break;
        }
    }
}
