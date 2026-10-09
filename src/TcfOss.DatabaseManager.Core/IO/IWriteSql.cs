using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.Formatting;

namespace TcfOss.DatabaseManager.Core.IO;

public partial interface IWriteSql
{
    public static readonly Regex StartsWithWhitespaceRegex = CreateStartsWithWhitespaceRegex();

    void ToSql(SqlTextWriter writer);

    void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    [GeneratedRegex(@"^\s+", RegexOptions.Compiled)]
    private static partial Regex CreateStartsWithWhitespaceRegex();
}
