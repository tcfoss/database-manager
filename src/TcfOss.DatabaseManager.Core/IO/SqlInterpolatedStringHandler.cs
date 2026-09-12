using System.Runtime.CompilerServices;

namespace TcfOss.DatabaseManager.Core.IO;

#pragma warning disable CS9113 // variable name is unread - Needed for interpolated string handler
[InterpolatedStringHandler]
public readonly ref struct SqlInterpolatedStringHandler(int literalLength, int formattedCount, SqlTextWriter writer)
{
#pragma warning restore CS9113 // variable name is unread
    private readonly SqlTextWriter _writer = writer;

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
